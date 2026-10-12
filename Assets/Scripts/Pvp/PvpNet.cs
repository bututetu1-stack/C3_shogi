using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 対人戦の通信（中継サーバー server/worker.js・server/local-relay.js と話す）。
/// 部屋コードで入り、相手への手（Msg）を送り合う。サーバーは中身を知らず、記録を残して相手に渡すだけ。
/// つなぎ直すとサーバーが記録を全部送り直すので、読み終えた分は飛ばす。
/// 受け取ったものは Update でメインスレッドに渡す（OnMessage・OnPeer・OnWelcome）。
/// </summary>
public class PvpNet : MonoBehaviour
{
    /// <summary>相手に送る手（使う欄だけ埋める。JsonUtility で文字にする）</summary>
    [Serializable]
    public class Msg
    {
        public string k;            // setup / team / place / move / pass / resign / hash / ready
        public string rule;         // setup: "Cost" / "Random"
        public int seed;            // setup
        public int first;           // setup: 先手の席（0 か 1）
        public string[] pieces;     // team
        public string[] upgrades;   // team
        public int[] cells;         // place: x,y,x,y…（編成の順）
        public int n;               // move / pass / hash: 何手目か
        public int fx, fy, tx, ty;  // move
        public int h;               // hash
    }

    [Serializable] private class Entry { public int from; public Msg m; }
    [Serializable] private class Envelope
    {
        public string t;
        public int seat;
        public bool peer;
        public bool connected;
        public int from;
        public Msg m;
        public Entry[] history;
        public string reason;
    }
    [Serializable] private class Outgoing { public Msg m; }

    public static PvpNet Instance { get; private set; }

    /// <summary>自分の席（0 は部屋を作った人、1 は入った人）。つながる前は -1</summary>
    public int Seat { get; private set; } = -1;
    public bool Connected { get; private set; }
    public bool PeerConnected { get; private set; }
    public string RoomCode { get; private set; }
    public string LastError { get; private set; }

    public event Action OnWelcome;
    public event Action<bool> OnPeer;
    public event Action<Msg> OnMessage;
    public event Action<string> OnClosed;

    private ClientWebSocket ws;
    private CancellationTokenSource cts;
    private readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
    private readonly ConcurrentQueue<string> closed = new ConcurrentQueue<string>();
    private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
    private string serverUrl;
    private string clientId;
    // 読み終えた相手の手の数（つなぎ直したとき、記録のうちここまでは飛ばす）
    private int peerRead;

    /// <summary>部の中継サーバー（Cloudflare Workers。2026-10-12 に置いた。置き方は server/README.md）</summary>
    public const string DefaultServer = "https://c3shogi-relay.bututetu1-e75.workers.dev";

    /// <summary>前に使ったサーバーのアドレス（設定に残す。入れていなければ部の中継サーバー）</summary>
    public static string SavedServer
    {
        get { return PlayerPrefs.GetString("pvp_server", DefaultServer); }
        set { PlayerPrefs.SetString("pvp_server", value ?? ""); PlayerPrefs.Save(); }
    }

    public static PvpNet Ensure()
    {
        if (Instance != null) return Instance;
        var obj = new GameObject("PvpNet");
        DontDestroyOnLoad(obj);
        Instance = obj.AddComponent<PvpNet>();
        return Instance;
    }

    /// <summary>4桁の部屋コードを作る（勝ち負けに関わらないので演出の乱数でよい）</summary>
    public static string NewRoomCode()
    {
        return UnityEngine.Random.Range(1000, 10000).ToString();
    }

    /// <summary>部屋に入る（部屋を作るのも同じ。先に入った人が席0）</summary>
    public async void Connect(string server, string code)
    {
        Close("つなぎ直し");
        serverUrl = (server ?? "").Trim().TrimEnd('/');
        if (serverUrl.StartsWith("https://")) serverUrl = "wss://" + serverUrl.Substring(8);
        else if (serverUrl.StartsWith("http://")) serverUrl = "ws://" + serverUrl.Substring(7);
        RoomCode = code;
        clientId = Guid.NewGuid().ToString("N").Substring(0, 16);
        peerRead = 0;
        Seat = -1;
        await Open();
    }

    /// <summary>切れたときに同じ部屋・同じ席へつなぎ直す</summary>
    public async void Reconnect()
    {
        if (string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(RoomCode)) return;
        await Open();
    }

    private async Task Open()
    {
        LastError = null;
        cts = new CancellationTokenSource();
        ws = new ClientWebSocket();
        try
        {
            var uri = new Uri(serverUrl + "/room/" + RoomCode + "?client=" + clientId);
            await ws.ConnectAsync(uri, cts.Token);
            Connected = true;
            _ = ReceiveLoop(ws, cts.Token);
        }
        catch (Exception e)
        {
            Connected = false;
            LastError = e.Message;
            closed.Enqueue("つながらない: " + e.Message);
        }
    }

    private async Task ReceiveLoop(ClientWebSocket socket, CancellationToken token)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();
        try
        {
            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close) break;
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage) continue;
                inbox.Enqueue(sb.ToString());
                sb.Clear();
            }
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested) closed.Enqueue(e.Message);
            return;
        }
        if (!token.IsCancellationRequested) closed.Enqueue("切れた");
    }

    /// <summary>相手に手を送る</summary>
    public async void Send(Msg m)
    {
        if (ws == null || ws.State != WebSocketState.Open) { LastError = "つながっていない"; return; }
        string json = JsonUtility.ToJson(new Outgoing { m = m });
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await sendLock.WaitAsync();
        try { await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token); }
        catch (Exception e) { LastError = e.Message; }
        finally { sendLock.Release(); }
    }

    public void Close(string reason)
    {
        Connected = false;
        PeerConnected = false;
        if (cts != null) cts.Cancel();
        if (ws != null)
        {
            try { ws.Abort(); } catch (Exception) { }
            ws.Dispose();
        }
        ws = null;
    }

    void Update()
    {
        while (inbox.TryDequeue(out string text)) Handle(text);
        while (closed.TryDequeue(out string why))
        {
            Connected = false;
            if (OnClosed != null) OnClosed(why);
        }
    }

    private void Handle(string text)
    {
        Envelope e;
        try { e = JsonUtility.FromJson<Envelope>(text); }
        catch (Exception) { return; }
        if (e == null) return;
        switch (e.t)
        {
            case "welcome":
                Seat = e.seat;
                PeerConnected = e.peer;
                if (OnWelcome != null) OnWelcome();
                // 記録のうち、まだ読んでいない相手の手を渡す
                int seen = 0;
                if (e.history != null)
                    foreach (var entry in e.history)
                    {
                        if (entry.from == Seat) continue;
                        if (seen++ < peerRead) continue;
                        Deliver(entry.m);
                    }
                break;
            case "peer":
                PeerConnected = e.connected;
                if (OnPeer != null) OnPeer(e.connected);
                break;
            case "msg":
                if (e.from != Seat) Deliver(e.m);
                break;
            case "error":
                LastError = e.reason;
                break;
        }
    }

    private void Deliver(Msg m)
    {
        if (m == null) return;
        peerRead++;
        if (OnMessage != null) OnMessage(m);
    }

    void OnDestroy()
    {
        Close("終わり");
        if (Instance == this) Instance = null;
    }
}
