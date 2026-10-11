// 同じ種どうしで2つの記録を比べる（AI の新旧など）。種ごとの差から、差の平均と揺れ（標準誤差）を出す。
// 使い方: node docs/balance/paired.js A.jsonl B.jsonl
// 出力: 共通の種の数、全クリア（A→B）、勝ち抜いた局の平均（A→B）と差 ±標準誤差、B が良かった種・悪かった種の数
const fs = require('fs');
const [fa, fb] = process.argv.slice(2);
if (!fa || !fb) { console.error('使い方: node docs/balance/paired.js A.jsonl B.jsonl'); process.exit(2); }
const load = f => {
  const m = new Map();
  for (const l of fs.readFileSync(f, 'utf8').split('\n')) if (l.trim()) { const r = JSON.parse(l); m.set(r.seed, r); }
  return m;
};
// 勝ち抜いた局の数（全クリアなら15、そうでなければ負けた局の1つ前まで）
const won = r => r.cleared ? r.reached : r.reached - 1;
const a = load(fa), b = load(fb);
const diffs = [];
let ca = 0, cb = 0, better = 0, worse = 0, wa = 0, wb = 0;
for (const [seed, ra] of a) {
  const rb = b.get(seed);
  if (!rb) continue;
  const d = won(rb) - won(ra);
  diffs.push(d);
  wa += won(ra); wb += won(rb);
  if (ra.cleared) ca++;
  if (rb.cleared) cb++;
  if (d > 0) better++; else if (d < 0) worse++;
}
const n = diffs.length;
if (!n) { console.log('共通の種がありません'); process.exit(0); }
const mean = diffs.reduce((s, x) => s + x, 0) / n;
const sd = Math.sqrt(diffs.reduce((s, x) => s + (x - mean) ** 2, 0) / Math.max(1, n - 1));
console.log(`共通の種 ${n}: 全クリア ${ca} → ${cb}（${(100 * ca / n).toFixed(0)}% → ${(100 * cb / n).toFixed(0)}%）`);
console.log(`勝ち抜いた局 ${(wa / n).toFixed(2)} → ${(wb / n).toFixed(2)}（差 ${mean >= 0 ? '+' : ''}${mean.toFixed(2)} ±${(sd / Math.sqrt(n)).toFixed(2)}）`);
console.log(`B のほうが先へ進んだ種 ${better}、手前で負けた種 ${worse}、同じ ${n - better - worse}`);
