// 提督の艦隊の戦いの長さを集計する（自動プレイの記録の fleet 欄から）。
// 使い方: node docs/balance/fleet_stats.js BalanceReports/v8/force_Teitoku.jsonl [ほかの .jsonl …]
// 出力: 着任から作戦完了・轟沈までの手数（両軍の手の合計）の分布、夜戦に入った割合、S/A 勝利の割合、艦種ごとの与ダメージ
const fs = require('fs');

const files = process.argv.slice(2);
const avg = xs => xs.reduce((a, b) => a + b, 0) / xs.length;
if (files.length === 0) { console.error('使い方: node docs/balance/fleet_stats.js <記録.jsonl> …'); process.exit(2); }

for (const file of files) {
  let arrivals = 0, nights = 0, s = 0, a = 0, sunk = 0, unfinished = 0;
  const ends = [];          // 作戦完了・轟沈までの手数
  const nightAt = [];
  const shipDamage = {};    // 艦種 → 作戦完了までに与えたダメージの一覧（"艦:駆逐艦@5"）
  for (const line of fs.readFileSync(file, 'utf8').split('\n')) {
    if (!line.trim()) continue;
    const run = JSON.parse(line);
    for (const st of run.stages || []) {
      const ev = (st.fleet || []).map(e => { const [k, v] = e.split('@'); return { k, v: +v }; });
      // 1つの局に艦隊は1つ（着任は1回）として数える
      const arrive = ev.find(e => e.k === '着任');
      if (!arrive) continue;
      arrivals++;
      for (const e of ev) {
        if (!e.k.startsWith('艦:')) continue;
        const cls = e.k.slice(2);
        (shipDamage[cls] = shipDamage[cls] || []).push(e.v);
      }
      const night = ev.find(e => e.k === '夜戦');
      if (night) { nights++; nightAt.push(night.v); }
      const end = ev.find(e => e.k === 'S' || e.k === 'A' || e.k === '轟沈');
      if (!end) { unfinished++; continue; }
      if (end.k === 'S') s++; else if (end.k === 'A') a++; else sunk++;
      ends.push(end.v);
    }
  }
  ends.sort((x, y) => x - y);
  const pct = (n, d) => d ? (100 * n / d).toFixed(0) + '%' : '-';
  const q = p => ends.length ? ends[Math.min(ends.length - 1, Math.floor(p * ends.length))] : '-';
  console.log(file);
  console.log('  着任 ' + arrivals + ' 回 / S勝利 ' + pct(s, arrivals) + ' / A勝利 ' + pct(a, arrivals) + ' / 提督轟沈 ' + pct(sunk, arrivals) + ' / 局が先に終わった ' + pct(unfinished, arrivals));
  console.log('  決着までの手数（両軍の合計）: 25% ' + q(0.25) + ' / 中央 ' + q(0.5) + ' / 75% ' + q(0.75) + ' / 90% ' + q(0.9));
  for (const n of [6, 8, 10, 12, 14, 20]) {
    const reach = ends.filter(v => v >= n).length + unfinished;
    console.log('  夜戦を ' + n + ' 手で始めたら入れる艦隊: ' + pct(reach, arrivals));
  }
  console.log('  実際に夜戦に入った: ' + pct(nights, arrivals));
  const classes = Object.keys(shipDamage);
  if (classes.length) {
    console.log('  作戦完了までに与えたダメージ（艦種ごとの平均、深海以外も含む）:');
    for (const c of classes.sort((x, y) => avg(shipDamage[y]) - avg(shipDamage[x])))
      console.log('    ' + c + ' ' + avg(shipDamage[c]).toFixed(1) + '（' + shipDamage[c].length + '回）');
  }
}
