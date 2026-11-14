// コマ撮り（docs/snippets/cap_frame.cs の Builds/Media/frames/NNN.rgb）からアニメーション GIF を作る。外部のパッケージは使わない。
// 使い方: node docs/media/make_gif.js <framesフォルダ> <出力.gif> <コマの範囲 例 40-75,100-200> [幅 640] [高さ 360] [1コマの長さ(1/100秒) 10]
// しくみ: 全コマから 255 色を選び（メディアンカット）、前のコマから変わった四角だけを書く（変わらない所は透明）。
const fs = require('fs');
const path = require('path');

const [dir, out, rangesArg, wArg, hArg, delayArg] = process.argv.slice(2);
if (!dir || !out || !rangesArg) { console.error('使い方: node docs/media/make_gif.js <framesフォルダ> <出力.gif> <40-75,100-200> [幅] [高さ] [1/100秒]'); process.exit(2); }
const W = +(wArg || 640), H = +(hArg || 360), DELAY = +(delayArg || 10);

// --- コマを読む（Unity の ReadPixels は下の行から並ぶので上下を返す） ---
const ids = [];
for (const part of rangesArg.split(',')) {
  const [a, b] = part.split('-').map(Number);
  for (let i = a; i <= (b === undefined ? a : b); i++) ids.push(i);
}
const frames = ids.map(i => {
  const raw = fs.readFileSync(path.join(dir, String(i).padStart(3, '0') + '.rgb'));
  if (raw.length !== W * H * 3) throw new Error('大きさが違う: ' + i);
  const px = Buffer.alloc(W * H * 3);
  for (let y = 0; y < H; y++) raw.copy(px, y * W * 3, (H - 1 - y) * W * 3, (H - y) * W * 3);
  return px;
});

// --- 255 色を選ぶ（メディアンカット。間引いた画素で） ---
const samples = [];
for (let f = 0; f < frames.length; f += 2)
  for (let p = 0; p < W * H; p += 7) samples.push([frames[f][p * 3], frames[f][p * 3 + 1], frames[f][p * 3 + 2]]);
let boxes = [samples];
while (boxes.length < 255) {
  let best = -1, bestScore = -1, bestCh = 0;
  boxes.forEach((box, i) => {
    if (box.length < 2) return;
    for (let c = 0; c < 3; c++) {
      let lo = 255, hi = 0;
      for (const s of box) { if (s[c] < lo) lo = s[c]; if (s[c] > hi) hi = s[c]; }
      const score = (hi - lo) * Math.sqrt(box.length);
      if (score > bestScore) { bestScore = score; best = i; bestCh = c; }
    }
  });
  if (best < 0 || bestScore <= 0) break;
  const box = boxes[best].sort((p, q) => p[bestCh] - q[bestCh]);
  const mid = box.length >> 1;
  boxes.splice(best, 1, box.slice(0, mid), box.slice(mid));
}
const palette = boxes.map(box => {
  const s = [0, 0, 0];
  for (const p of box) { s[0] += p[0]; s[1] += p[1]; s[2] += p[2]; }
  return s.map(v => Math.round(v / box.length));
});
while (palette.length < 255) palette.push([0, 0, 0]);
const TRANSPARENT = 255;
palette.push([0, 0, 0]);

// --- 色を番号にする（各色 6 ビットの表で、いちばん近い色） ---
const lut = new Uint8Array(1 << 18);
for (let k = 0; k < lut.length; k++) {
  const r = ((k >> 12) & 63) * 4 + 2, g = ((k >> 6) & 63) * 4 + 2, b = (k & 63) * 4 + 2;
  let best = 0, bd = Infinity;
  for (let i = 0; i < 255; i++) {
    const p = palette[i], dr = p[0] - r, dg = p[1] - g, db = p[2] - b;
    const d = dr * dr * 3 + dg * dg * 4 + db * db * 2;
    if (d < bd) { bd = d; best = i; }
  }
  lut[k] = best;
}
const indexed = frames.map(px => {
  const idx = new Uint8Array(W * H);
  for (let p = 0; p < W * H; p++) idx[p] = lut[((px[p * 3] >> 2) << 12) | ((px[p * 3 + 1] >> 2) << 6) | (px[p * 3 + 2] >> 2)];
  return idx;
});

// --- LZW ---
function lzw(data, minCode) {
  const bytes = [];
  let cur = 0, bits = 0;
  const put = (code, size) => { cur |= code << bits; bits += size; while (bits >= 8) { bytes.push(cur & 255); cur >>>= 8; bits -= 8; } };
  const clear = 1 << minCode, end = clear + 1;
  let size = minCode + 1, next = end + 1;
  let dict = new Map();
  put(clear, size);
  let prefix = data[0];
  for (let i = 1; i < data.length; i++) {
    const c = data[i], key = prefix * 256 + c;
    const hit = dict.get(key);
    if (hit !== undefined) { prefix = hit; continue; }
    put(prefix, size);
    if (next < 4096) {
      dict.set(key, next++);
      if (next > (1 << size) && size < 12) size++;
    } else {
      put(clear, size);
      dict = new Map(); size = minCode + 1; next = end + 1;
    }
    prefix = c;
  }
  put(prefix, size);
  put(end, size);
  if (bits > 0) bytes.push(cur & 255);
  const outBytes = [];
  for (let i = 0; i < bytes.length; i += 255) { const chunk = bytes.slice(i, i + 255); outBytes.push(chunk.length, ...chunk); }
  outBytes.push(0);
  return outBytes;
}

// --- GIF を書く ---
const buf = [];
const u16 = v => { buf.push(v & 255, (v >> 8) & 255); };
buf.push(...Buffer.from('GIF89a'));
u16(W); u16(H);
buf.push(0xF7, 0, 0);                       // 全体の色表 256 色
for (const c of palette) buf.push(c[0], c[1], c[2]);
buf.push(0x21, 0xFF, 11, ...Buffer.from('NETSCAPE2.0'), 3, 1, 0, 0, 0);   // くり返す
let prev = null;
indexed.forEach((idx, f) => {
  let x0 = 0, y0 = 0, x1 = W - 1, y1 = H - 1;
  if (prev) {
    x0 = W; y0 = H; x1 = -1; y1 = -1;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const p = y * W + x;
      if (idx[p] !== prev[p]) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
    }
    if (x1 < 0) { x0 = y0 = 0; x1 = y1 = 0; }   // 変化なし: 1画素だけ書く
  }
  const w = x1 - x0 + 1, h = y1 - y0 + 1;
  const sub = new Uint8Array(w * h);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const p = (y + y0) * W + (x + x0);
    sub[y * w + x] = prev && idx[p] === prev[p] ? TRANSPARENT : idx[p];
  }
  const delay = f === indexed.length - 1 ? DELAY * 8 : DELAY;      // 最後のコマは少し止める
  buf.push(0x21, 0xF9, 4, (1 << 2) | (prev ? 1 : 0)); u16(delay); buf.push(prev ? TRANSPARENT : 0, 0);
  buf.push(0x2C); u16(x0); u16(y0); u16(w); u16(h); buf.push(0);
  buf.push(8);
  for (const v of lzw(sub, 8)) buf.push(v);
  prev = idx;
});
buf.push(0x3B);
fs.writeFileSync(out, Buffer.from(buf));
console.log(out + ': ' + indexed.length + 'コマ、' + (buf.length / 1048576).toFixed(2) + 'MB、' + (indexed.length * DELAY / 100).toFixed(1) + '秒');
