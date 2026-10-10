// 自動プレイの結果を並べて比べる（勝ち抜いた局の平均・全クリア・局ごとの勝率と手数）
// 使い方: node docs/balance/compare.js <フォルダ> 名前1 名前2 ...   （<フォルダ>/<名前>.jsonl を読む）
//   例: node docs/balance/compare.js BalanceReports/check base   ／ 調整前後を同じ種で並べるときは、比べたい jsonl を1つのフォルダに名前を変えて置く
const fs = require('fs');
const dir = process.argv[2];
const names = process.argv.slice(3);
const isWin = x => x.outcome === 'Won' || x.outcome === 'JudgedWin';
const mean = a => a.reduce((s, v) => s + v, 0) / Math.max(1, a.length);
const sd = a => { const m = mean(a); return Math.sqrt(a.reduce((s, v) => s + (v - m) ** 2, 0) / Math.max(1, a.length - 1)); };
const res = {};
for (const nm of names) {
  const f = dir + '/' + nm + '.jsonl';
  if (!fs.existsSync(f)) continue;
  const m = new Map();
  fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map(JSON.parse).forEach(r => { if (!m.has(r.seed)) m.set(r.seed, r); });
  const runs = [...m.values()];
  const won = runs.map(r => r.stages.filter(isWin).length);
  const st = runs.flatMap(r => r.stages);
  const row = { n: runs.length, avgWon: mean(won), se: sd(won) / Math.sqrt(runs.length), cleared: runs.filter(r => r.cleared).length, sec: mean(runs.map(r => r.seconds)), stage: {} };
  for (let s = 1; s <= 15; s++) {
    const r = st.filter(x => x.stage === s); if (!r.length) continue;
    const w = r.filter(isWin);
    row.stage[s] = { w: w.length, n: r.length, moves: Math.round(mean(r.map(x => x.moves))), judged: r.filter(x => x.outcome.startsWith('Judged') || x.outcome === 'Timeout').length, c3: w.length ? +mean(w.map(x => x.playerC3HP)).toFixed(1) : null };
  }
  res[nm] = row;
}
console.log('name'.padEnd(10), 'n'.padStart(3), 'won'.padStart(6), '±se', 'clr', 'sec');
for (const [k, r] of Object.entries(res)) console.log(k.padEnd(10), String(r.n).padStart(3), r.avgWon.toFixed(2).padStart(6), r.se.toFixed(2), String(r.cleared).padStart(3), r.sec.toFixed(1));
console.log('\n局ごとの勝率（勝ち/挑戦）と平均手数');
console.log('局 ' + Object.keys(res).map(k => k.padStart(16)).join(''));
for (let s = 1; s <= 15; s++) console.log(String(s).padStart(2) + ' ' + Object.values(res).map(r => { const x = r.stage[s]; return (x ? `${x.w}/${x.n} ${x.moves}手` : '-').padStart(16); }).join(''));
if (process.argv.includes('--json')) fs.writeFileSync(dir + '/summary.json', JSON.stringify(res, null, 1));
