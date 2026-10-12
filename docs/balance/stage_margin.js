// 局ごとの「勝ち方」を比べる（AI の強さを、周の結果より揺れの小さい指標で見る）。
// 使い方: node docs/balance/stage_margin.js A.jsonl B.jsonl …
// 局ごとに: 勝率、勝った局の平均手数、自軍 C3 の残り体力の平均（勝った局）。最後に全局の平均
const fs = require('fs');
const files = process.argv.slice(2);
if (!files.length) { console.error('使い方: node docs/balance/stage_margin.js A.jsonl B.jsonl …'); process.exit(2); }

const stats = files.map(f => {
  const by = {};
  for (const l of fs.readFileSync(f, 'utf8').split('\n')) {
    if (!l.trim()) continue;
    for (const st of JSON.parse(l).stages || []) {
      const s = by[st.stage] = by[st.stage] || { n: 0, win: 0, moves: 0, hp: 0, left: 0 };
      s.n++;
      const won = st.outcome === 'Won' || st.outcome === 'JudgedWin';
      if (won) { s.win++; s.moves += st.moves; s.hp += st.playerC3HP; }
      else s.left += st.enemyLeft;
    }
  }
  return by;
});

const name = f => f.replace(/^.*BalanceReports[\\/]/, '');
console.log('局  ' + files.map(f => name(f).padEnd(30)).join(''));
const tot = files.map(() => ({ n: 0, win: 0, moves: 0, hp: 0 }));
for (let stage = 1; stage <= 15; stage++) {
  const cells = stats.map((by, i) => {
    const s = by[stage];
    if (!s) return '-'.padEnd(30);
    tot[i].n += s.n; tot[i].win += s.win; tot[i].moves += s.moves; tot[i].hp += s.hp;
    const w = s.win || 1;
    return `${(100 * s.win / s.n).toFixed(0).padStart(3)}% ${(s.moves / w).toFixed(1).padStart(5)}手 C3 ${(s.hp / w).toFixed(1)}`.padEnd(30);
  });
  console.log(String(stage).padStart(2) + '  ' + cells.join(''));
}
console.log('計  ' + tot.map(t => `${(100 * t.win / t.n).toFixed(1)}% ${(t.moves / t.win).toFixed(1)}手 C3 ${(t.hp / t.win).toFixed(2)}`.padEnd(30)).join(''));
