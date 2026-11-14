// 対人戦の通信を試すための相手役（Node だけで動く）。
// 部屋を作り（席0・先手）、ルールと先手を決めて送り、編成と並べ方を送り、自分の番には最初に歩兵を1つ進め、あとはパスする。
// 使い方: node server/local-relay.js 8787 を動かしてから
//         node server/test-bot.js ws://localhost:8787 4321
// Unity では「対人戦 → 部屋に入る」でサーバー ws://localhost:8787・部屋コード 4321 を入れる（Unity は後手になり、盤が回る）
const server = process.argv[2] || 'ws://localhost:8787';
const code = process.argv[3] || '4321';
const ws = new WebSocket(server + '/room/' + code + '?client=bot' + Date.now());
const send = m => { ws.send(JSON.stringify({ m })); console.log('→', JSON.stringify(m)); };
let mySeat = -1, setupSent = false, placed = false, moved = false;
const handled = new Set();

ws.onmessage = ev => {
  const e = JSON.parse(ev.data);
  if (e.t === 'welcome') { mySeat = e.seat; console.log('席', mySeat, '相手', e.peer); if (e.peer) start(); return; }
  if (e.t === 'peer') { console.log('相手', e.connected ? 'つながった' : '切れた'); if (e.connected) start(); return; }
  if (e.t !== 'msg') return;
  const m = e.m;
  console.log('←', JSON.stringify(m));
  if (m.k === 'team' && !placed) {
    placed = true;
    // 先手（下）の並べる順: 手前2段目の真ん中から外へ（PvpMatch.PlacementOrder と同じ）
    send({ k: 'place', cells: [4, 1, 3, 1, 5, 1, 2, 1] });
  }
  if (m.k === 'hash') {
    // 手番が変わった合図。奇数手目は先手（このボット）の番
    if (m.n % 2 === 1 && !handled.has(m.n)) {
      handled.add(m.n);
      setTimeout(() => {
        if (!moved) { moved = true; send({ k: 'move', n: m.n, fx: 4, fy: 1, tx: 4, ty: 2 }); }
        else send({ k: 'pass', n: m.n });
      }, 800);
    }
  }
  if (m.k === 'resign') { console.log('相手が投了した'); process.exit(0); }
};

function start() {
  if (setupSent) return;
  setupSent = true;
  send({ k: 'setup', rule: 'Cost', seed: 12345, first: 0 });
  send({ k: 'team', pieces: ['Pawn', 'Pawn', 'Gold', 'Yuu'], upgrades: [] });
}
ws.onclose = () => { console.log('切れた'); process.exit(0); };
