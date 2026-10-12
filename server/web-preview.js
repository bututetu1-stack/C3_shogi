// ブラウザ版（Builds/WebGL）を手元で開くための小さなサーバー。
// 使い方: node server/web-preview.js [ポート]  → http://localhost:8090
// Gzip のファイル（.gz）は Content-Encoding: gzip を付けて返す（unityroom と同じ形で読ませる）
const http = require('http');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..', 'Builds', 'WebGL');
const port = Number(process.argv[2]) || 8090;
const types = { '.html': 'text/html; charset=utf-8', '.js': 'application/javascript', '.wasm': 'application/wasm', '.data': 'application/octet-stream', '.css': 'text/css', '.png': 'image/png', '.ico': 'image/x-icon', '.json': 'application/json' };

http.createServer((req, res) => {
  let rel = decodeURIComponent(req.url.split('?')[0]);
  if (rel.endsWith('/')) rel += 'index.html';
  const file = path.join(root, rel);
  if (!file.startsWith(root) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) { res.writeHead(404); res.end('not found'); return; }
  const headers = { 'Cache-Control': 'no-cache' };
  let name = file;
  if (name.endsWith('.gz')) { headers['Content-Encoding'] = 'gzip'; name = name.slice(0, -3); }
  headers['Content-Type'] = types[path.extname(name)] || 'application/octet-stream';
  res.writeHead(200, headers);
  fs.createReadStream(file).pipe(res);
}).listen(port, () => console.log('ブラウザ版: http://localhost:' + port + '/'));
