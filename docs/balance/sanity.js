// 自動プレイの記録の検査（デバッグ用）。おかしな局と、AI の読みの統計を出す。
// 使い方: node docs/balance/sanity.js BalanceReports/exp/base.jsonl [ほかの .jsonl …]
// 見るもの:
//   - 終わり方の内訳（勝ち・判定・時間切れ）と、負け方（C3撃破・全滅・判定）
//   - 0手で終わった局、手数の上限（150手）を超えた局、C3 の体力がおかしい局、同じ種が2回ある周
//   - AI の読み: 深さ4以上まで読めた割合、平均の深さ、1手あたりの局面数と時間（陣営ごと）
const fs = require('fs');

const files = process.argv.slice(2);
if (!files.length) { console.error('使い方: node docs/balance/sanity.js <記録.jsonl> …'); process.exit(2); }
const MOVE_LIMIT = 150;
const pct = (n, d) => d ? (100 * n / d).toFixed(1) + '%' : '-';

let problems = 0;
for (const file of files) {
  const runs = fs.readFileSync(file, 'utf8').split('\n').filter(l => l.trim()).map(l => JSON.parse(l));
  const outcomes = {}, causes = {};
  const odd = [];
  const seeds = new Map();
  let stages = 0;
  for (const r of runs) {
    seeds.set(r.seed, (seeds.get(r.seed) || 0) + 1);
    if (!r.stages || !r.stages.length) odd.push(`種${r.seed}: 局の記録がない`);
    for (const st of r.stages || []) {
      stages++;
      outcomes[st.outcome] = (outcomes[st.outcome] || 0) + 1;
      if (st.lossCause) causes[st.lossCause] = (causes[st.lossCause] || 0) + 1;
      const where = `種${r.seed} 第${st.stage}局`;
      if (st.moves <= 0) odd.push(`${where}: 0手で終わった（${st.outcome}）`);
      if (st.moves > MOVE_LIMIT + 2) odd.push(`${where}: 手数 ${st.moves}（上限 ${MOVE_LIMIT} を超えた）`);
      if (st.outcome === 'Timeout') odd.push(`${where}: 時間切れ（決着しないまま打ち切り）`);
      if (st.playerC3HP < 0) odd.push(`${where}: 自軍 C3 の体力が ${st.playerC3HP}`);
      if ((st.outcome === 'Won') && st.enemyLeft > 0 && st.lossCause) odd.push(`${where}: 勝ったのに負け方が書いてある`);
      if ((st.kills || []).length !== (st.victims || []).length) odd.push(`${where}: 撃破の記録の数が合わない`);
    }
    if (r.cleared && r.reached !== 15) odd.push(`種${r.seed}: 全クリアなのに最後の局が ${r.reached}`);
  }
  for (const [seed, n] of seeds) if (n > 1) odd.push(`種${seed}: ${n}回ある`);

  console.log(file);
  console.log(`  ${runs.length}周・${stages}局。終わり方: ` + Object.entries(outcomes).map(([k, v]) => `${k} ${v}`).join(' / '));
  console.log('  負け方: ' + (Object.entries(causes).map(([k, v]) => `${k} ${v}`).join(' / ') || 'なし'));

  // AI の読み
  const sum = k => runs.reduce((a, r) => a + (r[k] || 0), 0);
  for (const side of ['enemy', 'player']) {
    const moves = sum(side + 'Moves');
    if (!moves) continue;
    const name = side === 'enemy' ? '敵' : '自軍';
    const depth = sum(side + 'DepthSum');
    const nodes = sum(side + 'Nodes');
    const ms = sum(side + 'Ms');
    console.log(`  ${name}の AI: ${moves}手、深さ4以上 ${pct(sum(side + 'DeepMoves'), moves)}` +
      (depth ? `、平均の深さ ${(depth / moves).toFixed(2)}` : '') +
      (nodes ? `、1手 ${Math.round(nodes / moves)}局面・${(ms / moves).toFixed(1)}ms` : ''));
  }
  const secs = runs.map(r => r.seconds).sort((a, b) => a - b);
  console.log(`  1周の時間: 中央 ${secs[Math.floor(secs.length / 2)].toFixed(1)}秒・最長 ${secs[secs.length - 1].toFixed(1)}秒`);

  if (odd.length) {
    problems += odd.length;
    console.log(`  おかしな所 ${odd.length}件:`);
    for (const o of odd.slice(0, 20)) console.log('    ' + o);
    if (odd.length > 20) console.log(`    …ほか ${odd.length - 20}件`);
  } else {
    console.log('  おかしな所はなし');
  }
}
process.exit(0);
