using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// オンラインの対人戦の進め方（PvpNet の上で、何を送り合うか）。
///   部屋を作った人（席0）がルール・種・先手を決めて送る → それぞれ自分の編成を送る → 自分の並べ方を送る → 対局。
///   対局中は自分の手（move・pass・resign）を送り、相手の手が届いたら同じ順番で再生する（GameRng の種が同じなので同じ盤になる）。
///   手番が変わるたびに盤の要約（hash）を送り合い、ずれていたら知らせる。
/// </summary>
public class PvpOnline : MonoBehaviour
{
    public static PvpOnline Instance { get; private set; }

    private PvpNet net;
    public PvpNet.Msg Setup { get; private set; }
    public PvpNet.Msg PeerTeam { get; private set; }
    public PvpNet.Msg PeerPlace { get; private set; }
    private readonly Queue<PvpNet.Msg> actions = new Queue<PvpNet.Msg>();
    private readonly Dictionary<int, int> myHashes = new Dictionary<int, int>();
    private readonly Dictionary<int, int> peerHashes = new Dictionary<int, int>();
    private bool running;
    private bool desyncReported;
    private int reconnectTries;

    public static PvpOnline Ensure(PvpNet net)
    {
        if (Instance == null)
        {
            var obj = new GameObject("PvpOnline");
            DontDestroyOnLoad(obj);
            Instance = obj.AddComponent<PvpOnline>();
        }
        Instance.Attach(net);
        return Instance;
    }

    private void Attach(PvpNet n)
    {
        if (net != null)
        {
            net.OnMessage -= OnMessage;
            net.OnPeer -= OnPeer;
            net.OnClosed -= OnClosed;
        }
        net = n;
        net.OnMessage += OnMessage;
        net.OnPeer += OnPeer;
        net.OnClosed += OnClosed;
        Setup = PeerTeam = PeerPlace = null;
        actions.Clear();
        myHashes.Clear();
        peerHashes.Clear();
        running = false;
        desyncReported = false;
        reconnectTries = 0;
    }

    private void OnMessage(PvpNet.Msg m)
    {
        switch (m.k)
        {
            case "setup": Setup = m; break;
            case "team": PeerTeam = m; break;
            case "place": PeerPlace = m; break;
            case "move":
            case "pass":
            case "resign": actions.Enqueue(m); break;
            case "hash":
                peerHashes[m.n] = m.h;
                Compare(m.n);
                break;
        }
    }

    private void OnPeer(bool connected)
    {
        if (BattleLogUI.Instance == null) return;
        BattleLogUI.Instance.AddLog(connected ? "相手がつながった" : "相手との接続が切れた（つなぎ直すのを待っています）");
    }

    private void OnClosed(string why)
    {
        if (!PvpMatch.Active || !PvpMatch.Online) return;
        if (BattleLogUI.Instance != null) BattleLogUI.Instance.AddLog("サーバーとの接続が切れた（" + why + "）。つなぎ直します");
        if (reconnectTries++ < 20) StartCoroutine(ReconnectLater());
    }

    private IEnumerator ReconnectLater()
    {
        yield return new WaitForSeconds(2f);
        if (net != null && !net.Connected) net.Reconnect();
    }

    // ------------------------------------------------------------
    // 送る
    // ------------------------------------------------------------

    /// <summary>部屋を作った人: ルール・種・先手の席を決めて送る（自分でも同じものを使う）</summary>
    public void SendSetup(PvpMatch.Rule rule)
    {
        var m = new PvpNet.Msg
        {
            k = "setup",
            rule = rule.ToString(),
            seed = Environment.TickCount & 0x7fffffff,
            first = UnityEngine.Random.Range(0, 2),
        };
        Setup = m;
        net.Send(m);
    }

    public void SendTeam(PvpMatch.Side side)
    {
        var m = new PvpNet.Msg { k = "team" };
        m.pieces = side.pieces.ConvertAll(p => p.pieceType.ToString()).ToArray();
        m.upgrades = side.upgrades.ConvertAll(u => u.ToString()).ToArray();
        net.Send(m);
    }

    public void SendPlacement(List<PieceInstance> pieces)
    {
        var cells = new int[pieces.Count * 2];
        for (int i = 0; i < pieces.Count; i++)
        {
            cells[i * 2] = pieces[i].boardPosition.x;
            cells[i * 2 + 1] = pieces[i].boardPosition.y;
        }
        net.Send(new PvpNet.Msg { k = "place", cells = cells });
    }

    public void SendMove(Vector2Int from, Vector2Int to)
    {
        int n = GameManager.Instance != null ? GameManager.Instance.MoveCount : 0;
        net.Send(new PvpNet.Msg { k = "move", n = n, fx = from.x, fy = from.y, tx = to.x, ty = to.y });
    }

    public void SendPass()
    {
        int n = GameManager.Instance != null ? GameManager.Instance.MoveCount : 0;
        net.Send(new PvpNet.Msg { k = "pass", n = n });
    }

    public void SendResign()
    {
        net.Send(new PvpNet.Msg { k = "resign" });
    }

    // ------------------------------------------------------------
    // 受け取ったものを盤に入れる
    // ------------------------------------------------------------

    /// <summary>相手の編成を、その陣営に入れる</summary>
    public static void ApplyTeam(PvpNet.Msg m, PvpMatch.Side side)
    {
        side.pieces.Clear();
        side.upgrades.Clear();
        BoardManager bm = BoardManager.Instance;
        foreach (string name in m.pieces ?? new string[0])
        {
            PieceType t;
            if (Enum.TryParse(name, out t) && bm != null)
            {
                PieceData d = bm.GetPieceDataByType(t);
                if (d != null) side.pieces.Add(d);
            }
        }
        foreach (string name in m.upgrades ?? new string[0])
        {
            UpgradeKind u;
            if (Enum.TryParse(name, out u)) side.upgrades.Add(u);
        }
    }

    /// <summary>相手の並べ方を盤に入れる（編成の順に、その駒の行き先）</summary>
    public static void ApplyPlacement(PvpNet.Msg m, List<PieceInstance> pieces)
    {
        if (m.cells == null) return;
        BoardManager bm = BoardManager.Instance;
        for (int i = 0; i < pieces.Count && i * 2 + 1 < m.cells.Length; i++)
        {
            var target = new Vector2Int(m.cells[i * 2], m.cells[i * 2 + 1]);
            PieceInstance p = pieces[i];
            if (!bm.IsInBounds(target) || p.boardPosition == target) continue;
            PieceInstance there = bm.GetPieceAt(target);
            if (there != null && there.team != p.team) continue;   // おかしな並べ方は無視
            bm.SwapPieces(p.boardPosition, target);
        }
    }

    // ------------------------------------------------------------
    // 対局中: 相手の手を再生する
    // ------------------------------------------------------------

    void Update()
    {
        GameManager gm = GameManager.Instance;
        if (!PvpMatch.Active || !PvpMatch.Online || gm == null || running) return;
        if (gm.currentPhase != GamePhase.Battle || actions.Count == 0) return;
        PvpNet.Msg next = actions.Peek();
        if (next.k == "resign")
        {
            actions.Dequeue();
            gm.RemoteResign();
            return;
        }
        if (gm.currentTurn == PvpMatch.LocalTeam || gm.IsTurnProcessing) return;
        actions.Dequeue();
        if (next.k == "pass")
        {
            if (BattleLogUI.Instance != null) BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("相手", gm.currentTurn) + " はパスした");
            gm.EndTurn();
            return;
        }
        StartCoroutine(PlayRemoteMove(next));
    }

    private IEnumerator PlayRemoteMove(PvpNet.Msg m)
    {
        running = true;
        GameManager gm = GameManager.Instance;
        BoardManager bm = BoardManager.Instance;
        PieceInstance piece = bm.GetPieceAt(new Vector2Int(m.fx, m.fy));
        var to = new Vector2Int(m.tx, m.ty);
        MoveValidator.MoveResult? move = null;
        if (piece != null && piece.team == gm.currentTurn)
            foreach (var mv in MoveValidator.GetValidMoves(piece))
                if (mv.position == to) { move = mv; break; }
        if (!move.HasValue)
        {
            ReportDesync("相手の手（" + m.fx + "," + m.fy + "→" + m.tx + "," + m.ty + "）がこちらの盤では指せない");
            running = false;
            yield break;
        }
        if (InputManager.Instance != null) InputManager.Instance.ClearSelection();
        yield return CombatResolver.ExecuteMove(piece, move.Value);
        // 再行動（ゾーン）が付いたら、続きの手（move か pass）を待つ
        PieceInstance again = gm.TakeBonusMove();
        if (again == null) gm.EndTurn();
        running = false;
    }

    // ------------------------------------------------------------
    // 盤の要約（ずれの検出）
    // ------------------------------------------------------------

    /// <summary>手番が変わったとき（両方のゲームで同じ時点）に盤の要約を送る</summary>
    public void OnTurnChanged()
    {
        if (!PvpMatch.Active || !PvpMatch.Online || GameManager.Instance == null) return;
        int n = GameManager.Instance.MoveCount;
        int h = BoardHash();
        myHashes[n] = h;
        net.Send(new PvpNet.Msg { k = "hash", n = n, h = h });
        Compare(n);
    }

    private void Compare(int n)
    {
        int mine, theirs;
        if (!myHashes.TryGetValue(n, out mine) || !peerHashes.TryGetValue(n, out theirs)) return;
        if (mine != theirs) ReportDesync(n + "手目の盤が相手と食い違っている");
    }

    private void ReportDesync(string what)
    {
        Debug.LogWarning("[対人戦] ずれ: " + what);
        if (desyncReported) return;
        desyncReported = true;
        if (BattleLogUI.Instance != null) BattleLogUI.Instance.AddLog("※ " + what + "（不具合です。この対局の流れを教えてください）");
    }

    /// <summary>盤の要約（駒の種類・陣営・位置・体力・攻撃・防御・成り）</summary>
    public static int BoardHash()
    {
        BoardManager bm = BoardManager.Instance;
        int size = bm.CurrentBoardSize;
        unchecked
        {
            int h = 17;
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                {
                    PieceInstance p = bm.GetPieceAt(new Vector2Int(x, y));
                    if (p == null) continue;
                    h = h * 31 + x;
                    h = h * 31 + y;
                    h = h * 31 + (int)p.data.pieceType;
                    h = h * 31 + (int)p.team;
                    h = h * 31 + p.currentHP;
                    h = h * 31 + p.ATK;
                    h = h * 31 + p.DEF;
                    h = h * 31 + (p.isPromoted ? 1 : 0);
                }
            return h;
        }
    }

    public void Leave()
    {
        if (net != null) net.Close("退出");
    }
}
