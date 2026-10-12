// 手元で試すための中継サーバー（server/worker.js と同じ通信。Node だけで動き、追加の部品は要らない）。
// 使い方: node server/local-relay.js [ポート]   → ゲームの「サーバーのアドレス」に ws://localhost:8787 を入れる
const http = require('http');
const crypto = require('crypto');

const PORT = +(process.argv[2] || 8787);
const MAX_MESSAGE = 4096;
const rooms = new Map();   // 部屋コード → { clients:[id,id], sockets:[sock,sock], history:[] }

function room(code) {
  if (!rooms.has(code)) rooms.set(code, { clients: [null, null], sockets: [null, null], history: [] });
  return rooms.get(code);
}

// ---- WebSocket の最小の実装（テキストと閉じる・ping だけ） ----
function sendFrame(sock, text, opcode = 1) {
  if (!sock || sock.destroyed) return;
  const payload = Buffer.from(text, 'utf8');
  let head;
  if (payload.length < 126) head = Buffer.from([0x80 | opcode, payload.length]);
  else if (payload.length < 65536) { head = Buffer.alloc(4); head[0] = 0x80 | opcode; head[1] = 126; head.writeUInt16BE(payload.length, 2); }
  else { head = Buffer.alloc(10); head[0] = 0x80 | opcode; head[1] = 127; head.writeBigUInt64BE(BigInt(payload.length), 2); }
  sock.write(Buffer.concat([head, payload]));
}
const send = (sock, obj) => sendFrame(sock, JSON.stringify(obj));

function onFrames(sock, onText, onClose) {
  let buf = Buffer.alloc(0);
  sock.on('data', chunk => {
    buf = Buffer.concat([buf, chunk]);
    while (buf.length >= 2) {
      const opcode = buf[0] & 0x0f;
      const masked = (buf[1] & 0x80) !== 0;
      let len = buf[1] & 0x7f, off = 2;
      if (len === 126) { if (buf.length < 4) return; len = buf.readUInt16BE(2); off = 4; }
      else if (len === 127) { if (buf.length < 10) return; len = Number(buf.readBigUInt64BE(2)); off = 10; }
      const maskOff = off; if (masked) off += 4;
      if (buf.length < off + len) return;
      let payload = buf.slice(off, off + len);
      if (masked) { const mask = buf.slice(maskOff, maskOff + 4); payload = Buffer.from(payload.map((b, i) => b ^ mask[i % 4])); }
      buf = buf.slice(off + len);
      if (opcode === 1) onText(payload.toString('utf8'));
      else if (opcode === 8) { try { sendFrame(sock, '', 8); } catch (e) {} sock.end(); onClose(); return; }
      else if (opcode === 9) sendFrame(sock, payload.toString('utf8'), 10);
    }
  });
  sock.on('close', onClose);
  sock.on('error', onClose);
}

const server = http.createServer((req, res) => {
  res.writeHead(200, { 'content-type': 'text/plain; charset=utf-8' });
  res.end('C3将棋 対人戦の中継サーバー（手元用）');
});

server.on('upgrade', (req, sock) => {
  const url = new URL(req.url, 'http://localhost');
  const m = url.pathname.match(/^\/room\/([A-Za-z0-9]{4,8})$/);
  const client = (url.searchParams.get('client') || '').slice(0, 64);
  if (!m || !client) { sock.end('HTTP/1.1 400 Bad Request\r\n\r\n'); return; }
  const r = room(m[1].toUpperCase());
  let seat = r.clients.indexOf(client);
  if (seat < 0) seat = r.clients.indexOf(null);
  if (seat < 0) { sock.end('HTTP/1.1 409 Conflict\r\n\r\n'); return; }
  const accept = crypto.createHash('sha1').update(req.headers['sec-websocket-key'] + '258EAFA5-E914-47DA-95CA-C5AB0DC85B11').digest('base64');
  sock.write('HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: ' + accept + '\r\n\r\n');
  if (r.sockets[seat]) { try { r.sockets[seat].end(); } catch (e) {} }
  r.clients[seat] = client;
  r.sockets[seat] = sock;
  console.log('部屋 ' + m[1] + ' 席' + seat + ' に ' + client + ' が入った');
  send(sock, { t: 'welcome', seat, peer: !!r.sockets[1 - seat], history: r.history });
  send(r.sockets[1 - seat], { t: 'peer', connected: true });
  let closed = false;
  onFrames(sock, text => {
    if (text.length > MAX_MESSAGE) return;
    let parsed; try { parsed = JSON.parse(text); } catch (e) { return; }
    if (!parsed || typeof parsed.m !== 'object' || parsed.m === null) return;
    r.history.push({ from: seat, m: parsed.m });
    send(r.sockets[1 - seat], { t: 'msg', from: seat, m: parsed.m });
  }, () => {
    if (closed) return;
    closed = true;
    if (r.sockets[seat] === sock) { r.sockets[seat] = null; send(r.sockets[1 - seat], { t: 'peer', connected: false }); }
    console.log('部屋 ' + m[1] + ' 席' + seat + ' が切れた');
  });
});

server.listen(PORT, () => console.log('中継サーバー（手元用）: ws://localhost:' + PORT + '/room/<部屋コード>?client=<ID>'));
