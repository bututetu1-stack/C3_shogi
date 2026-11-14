// 対人戦（AIどうし）の自動プレイの集計。
// 使い方: node docs/balance/pvp_stats.js 記録.jsonl [ほかの記録 …]
// 出すもの: 先手・後手の勝率、引き分け、決まり方、手数、使ったコスト。
//           駒・強化ごとに「編成に入っていた側の勝率」（引き分けは半分の勝ち）と、その札の多い・少ないの目安
const fs = require('fs');
const files = process.argv.slice(2);
if (!files.length) { console.error('使い方: node docs/balance/pvp_stats.js 記録.jsonl …'); process.exit(2); }
const pct = (a, b) => b ? (100 * a / b).toFixed(0) + '%' : '-';

for (const f of files) {
  const recs = fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
  if (!recs.length) continue;
  const n = recs.length;
  const first = recs.filter(r => r.winner === 'First').length;
  const second = recs.filter(r => r.winner === 'Second').length;
  const draw = n - first - second;
  const reasons = {};
  for (const r of recs) reasons[r.reason] = (reasons[r.reason] || 0) + 1;
  const avg = k => (recs.reduce((a, r) => a + r[k], 0) / n).toFixed(1);
  console.log(f + '（' + n + '局、ルール ' + recs[0].rule + '）');
  console.log('  先手の勝ち ' + pct(first, n) + '・後手の勝ち ' + pct(second, n) + '・引き分け ' + pct(draw, n)
    + '  手数 平均' + avg('moves') + '  1局 ' + avg('seconds') + '秒');
  console.log('  決まり方: ' + Object.entries(reasons).map(([k, v]) => k + ' ' + v).join('・'));
  if (recs[0].rule === 'Cost') console.log('  使ったコスト 先手' + avg('firstCost') + '・後手' + avg('secondCost'));

  // 札ごと: 入っていた側の勝ち点（勝ち1・引き分け0.5）
  const stat = {};
  const add = (name, won) => { const s = stat[name] = stat[name] || { n: 0, w: 0 }; s.n++; s.w += won; };
  for (const r of recs) {
    const firstScore = r.winner === 'First' ? 1 : r.winner === 'Draw' ? 0.5 : 0;
    for (const [pieces, ups, score] of [[r.firstPieces, r.firstUpgrades, firstScore], [r.secondPieces, r.secondUpgrades, 1 - firstScore]]) {
      for (const p of new Set(pieces)) add(p, score);
      for (const u of new Set(ups)) add('強化:' + u, score);
    }
  }
  const rows = Object.entries(stat).filter(([, s]) => s.n >= 8).map(([k, s]) => [k, s.n, s.w / s.n]).sort((a, b) => b[2] - a[2]);
  console.log('  札ごとの勝ち点（入っていた側。50%がふつう、8回以上だけ）:');
  for (const [k, cnt, rate] of rows) {
    const mark = rate >= 0.6 ? '  ← 強い（コストを上げる候補）' : rate <= 0.4 ? '  ← 弱い（コストを下げる候補）' : '';
    console.log('    ' + k.padEnd(16) + String(cnt).padStart(4) + '回  ' + (100 * rate).toFixed(0).padStart(3) + '%' + mark);
  }
}
