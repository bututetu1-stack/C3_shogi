// 部員の練度（★）の伸び方を集計する（自動プレイの記録の growth 欄から）。
// 使い方: node docs/balance/growth_stats.js BalanceReports/v10/base.jsonl [ほかの .jsonl …]
// 出力: 部員ごとに、★1・★2・★3 に届いた局の平均（届いた周の割合）と、局ごとの「★3の部員の数」の平均、鍛える札を選んだ回数
const fs = require('fs');

const files = process.argv.slice(2);
if (files.length === 0) { console.error('使い方: node docs/balance/growth_stats.js <記録.jsonl> …'); process.exit(2); }

for (const file of files) {
  const reach = {};        // 名前 → { owned: 周数, star: [[局…], [局…], [局…]] }
  const star3ByStage = {}; // 局 → [★3の部員の数…]
  let trains = 0, runs = 0;
  for (const line of fs.readFileSync(file, 'utf8').split('\n')) {
    if (!line.trim()) continue;
    const run = JSON.parse(line);
    runs++;
    const first = {};      // 名前 → [★1の局, ★2の局, ★3の局]
    for (const st of run.stages || []) {
      if ((st.pick || '').startsWith('鍛:')) trains++;
      if ((st.pick2 || '').startsWith('鍛:')) trains++;
      let star3 = 0;
      for (const g of st.growth || []) {
        const [name, s] = g.split(':');
        const stars = +s.replace('★', '');
        first[name] = first[name] || [null, null, null];
        for (let k = 0; k < stars; k++) if (first[name][k] === null) first[name][k] = st.stage;
        if (stars >= 3) star3++;
      }
      (star3ByStage[st.stage] = star3ByStage[st.stage] || []).push(star3);
    }
    for (const name in first) {
      reach[name] = reach[name] || { owned: 0, star: [[], [], []] };
      reach[name].owned++;
      for (let k = 0; k < 3; k++) if (first[name][k] !== null) reach[name].star[k].push(first[name][k]);
    }
  }
  const avg = xs => xs.length ? (xs.reduce((a, b) => a + b, 0) / xs.length).toFixed(1) : '-';
  console.log(file + '（' + runs + '周、鍛える札 ' + trains + '回）');
  console.log('  部員        仲間にした周  ★1の局（届いた割合）  ★2の局  ★3の局');
  for (const name of Object.keys(reach).sort((a, b) => reach[b].owned - reach[a].owned)) {
    const r = reach[name];
    const cell = k => avg(r.star[k]) + '（' + Math.round(100 * r.star[k].length / r.owned) + '%）';
    console.log('  ' + name.padEnd(8, '　') + String(r.owned).padStart(6) + '      ' + cell(0) + '   ' + cell(1) + '   ' + cell(2));
  }
  const stages = Object.keys(star3ByStage).map(Number).sort((a, b) => a - b);
  console.log('  局ごとの★3の部員の数（平均）: ' + stages.map(s => s + ':' + avg(star3ByStage[s])).join(' '));
}
