// C3将棋 対人戦の中継サーバー（Cloudflare Workers + Durable Objects、無料の枠で動く）。
//
// しくみ: 部屋コードごとに Durable Object（Room）を1つ作り、2人の WebSocket をつなぐ。
// サーバーはゲームの中身を知らない。届いた手をもう1人に送り、記録（history）に残すだけ。
// つなぎ直した人には、それまでの記録を全部送り直す（ゲームの側で、読み終えた分は飛ばす）。
//
// 通信（JSON 文字列）:
//   入る: wss://<アドレス>/room/<部屋コード>?client=<その人のID>
//   サーバー → 人: {t:"welcome", seat:0|1, peer:bool, history:[{from, m}...]}
//                  {t:"peer", connected:bool}          相手が入った・切れた
//                  {t:"msg", from:0|1, m:{...}}        相手からの手（m はゲームが決める）
//                  {t:"error", reason}
//   人 → サーバー: {m:{...}}                          相手に送る手
//
// 置き方は server/README.md。手元で試すときは server/local-relay.js（同じ通信の Node 版）。

const MAX_MESSAGE = 4096;      // 1つの手の大きさの上限（文字）
const MAX_HISTORY = 4000;      // 1部屋の記録の上限
const ROOM_TTL_MS = 6 * 60 * 60 * 1000;   // 6時間さわられなかった部屋は片付ける

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname === '/' || url.pathname === '/health') {
      return new Response('C3将棋 対人戦の中継サーバー（動いています）', { headers: { 'content-type': 'text/plain; charset=utf-8' } });
    }
    const m = url.pathname.match(/^\/room\/([A-Za-z0-9]{4,8})$/);
    if (!m) return new Response('not found', { status: 404 });
    if (request.headers.get('Upgrade') !== 'websocket') return new Response('expected websocket', { status: 426 });
    const id = env.ROOMS.idFromName(m[1].toUpperCase());
    return env.ROOMS.get(id).fetch(request);
  },
};

export class Room {
  constructor(state, env) {
    this.state = state;
    this.env = env;
  }

  async loadMeta() {
    const meta = (await this.state.storage.get('meta')) || { clients: [null, null], count: 0, touched: Date.now() };
    return meta;
  }

  async saveMeta(meta) {
    meta.touched = Date.now();
    await this.state.storage.put('meta', meta);
    await this.state.storage.setAlarm(Date.now() + ROOM_TTL_MS);
  }

  async fetch(request) {
    const url = new URL(request.url);
    const client = (url.searchParams.get('client') || '').slice(0, 64);
    if (!client) return new Response('client id required', { status: 400 });

    const meta = await this.loadMeta();
    let seat = meta.clients.indexOf(client);
    if (seat < 0) seat = meta.clients.indexOf(null);
    if (seat < 0) return new Response('room full', { status: 409 });
    meta.clients[seat] = client;
    await this.saveMeta(meta);

    // 同じ席の古いつながりは閉じる（つなぎ直し）
    for (const old of this.state.getWebSockets('seat' + seat)) {
      try { old.close(1000, 'replaced'); } catch (e) { /* もう閉じている */ }
    }

    const pair = new WebSocketPair();
    const [clientSide, serverSide] = Object.values(pair);
    this.state.acceptWebSocket(serverSide, ['seat' + seat]);

    const history = [];
    if (meta.count > 0) {
      const keys = [];
      for (let i = 0; i < meta.count; i++) keys.push('h' + i);
      // storage.get は一度に128キーまで
      for (let i = 0; i < keys.length; i += 128) {
        const got = await this.state.storage.get(keys.slice(i, i + 128));
        for (const k of keys.slice(i, i + 128)) if (got.has(k)) history.push(got.get(k));
      }
    }
    const peer = this.state.getWebSockets('seat' + (1 - seat)).length > 0;
    serverSide.send(JSON.stringify({ t: 'welcome', seat, peer, history }));
    this.sendTo(1 - seat, { t: 'peer', connected: true });
    return new Response(null, { status: 101, webSocket: clientSide });
  }

  seatOf(ws) {
    const tags = this.state.getTags(ws);
    return tags.includes('seat0') ? 0 : tags.includes('seat1') ? 1 : -1;
  }

  sendTo(seat, obj) {
    for (const ws of this.state.getWebSockets('seat' + seat)) {
      try { ws.send(JSON.stringify(obj)); } catch (e) { /* 閉じかけ */ }
    }
  }

  async webSocketMessage(ws, data) {
    const seat = this.seatOf(ws);
    if (seat < 0 || typeof data !== 'string' || data.length > MAX_MESSAGE) return;
    let parsed;
    try { parsed = JSON.parse(data); } catch (e) { return; }
    if (!parsed || typeof parsed.m !== 'object' || parsed.m === null) return;
    const meta = await this.loadMeta();
    if (meta.count >= MAX_HISTORY) { ws.send(JSON.stringify({ t: 'error', reason: 'history full' })); return; }
    const entry = { from: seat, m: parsed.m };
    await this.state.storage.put('h' + meta.count, entry);
    meta.count++;
    await this.saveMeta(meta);
    this.sendTo(1 - seat, { t: 'msg', from: seat, m: parsed.m });
  }

  async webSocketClose(ws) {
    const seat = this.seatOf(ws);
    if (seat < 0) return;
    // 同じ席に新しいつながりが残っていなければ、相手に知らせる
    const left = this.state.getWebSockets('seat' + seat).filter(s => s !== ws);
    if (left.length === 0) this.sendTo(1 - seat, { t: 'peer', connected: false });
  }

  async webSocketError(ws) {
    await this.webSocketClose(ws);
  }

  async alarm() {
    const meta = await this.loadMeta();
    if (Date.now() - meta.touched >= ROOM_TTL_MS && this.state.getWebSockets().length === 0) {
      await this.state.storage.deleteAll();
    } else {
      await this.state.storage.setAlarm(Date.now() + ROOM_TTL_MS);
    }
  }
}
