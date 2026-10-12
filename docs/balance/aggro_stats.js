// 敵がどれだけ自軍の駒を倒しに来るか・仲間の候補のレアの割合を比べる。
// 使い方: node docs/balance/aggro_stats.js A.jsonl B.jsonl …
// 出すもの: 1局あたりの自軍の駒の損失（全部・部員だけ）、勝った局の自軍 C3 の残り体力、
//           候補に出た部員のレアの割合（第一〜三局・第七〜九局・第十三〜十五局。BalanceReports/pieces_dump.json の今のレア）
const fs = require('fs');
const files = process.argv.slice(2);
if (!files.length) { console.error('使い方: node docs/balance/aggro_stats.js A.jsonl B.jsonl …'); process.exit(2); }
let rarity = {};
try {
  for (const p of JSON.parse(fs.readFileSync('BalanceReports/pieces_dump.json', 'utf8'))) rarity[p.full] = p.rarity, rarity[p.name] = p.rarity;
} catch (e) { /* なければレアの割合は出さない */ }
const band = s => s <= 3 ? '1-3' : s >= 7 && s <= 9 ? '7-9' : s >= 13 ? '13-15' : null;
const pct = (a, b) => b ? (100 * a / b).toFixed(0) + '%' : '-';
for (const f of files) {
  let stages = 0, lost = 0, lostMembers = 0, wins = 0, c3 = 0, runs = 0, clears = 0, reached = 0;
  const offer = {};
  for (const l of fs.readFileSync(f, 'utf8').split('\n')) {
    if (!l.trim()) continue;
    const r = JSON.parse(l);
    runs++; reached += r.reached; if (r.cleared) clears++;
    for (const st of r.stages || []) {
      stages++;
      const roster = new Set(st.roster || []);
      lost += (st.losses || []).length;
      lostMembers += (st.losses || []).filter(n => roster.has(n)).length;
      if (st.outcome === 'Won' || st.outcome === 'JudgedWin') { wins++; c3 += st.playerC3HP || 0; }
      const b = band(st.stage);
      if (b) for (const o of st.offered || []) {
        const rr = rarity[o]; if (!rr) continue;
        const k = offer[b] = offer[b] || { Bronze: 0, Rare: 0, SuperRare: 0, n: 0 };
        if (k[rr] != null) k[rr]++; k.n++;
      }
    }
  }
  console.log(f);
  console.log('  ' + runs + '周  平均の到達局 ' + (reached / runs).toFixed(2) + '  全クリア ' + pct(clears, runs));
  console.log('  1局あたりの自軍の損失 ' + (lost / stages).toFixed(2) + '（部員 ' + (lostMembers / stages).toFixed(2) + '）  勝った局の自軍C3の残り体力 ' + (c3 / Math.max(1, wins)).toFixed(2));
  const bands = Object.keys(offer);
  if (bands.length) console.log('  候補のレア（ブロンズ/レア/激レア）: ' + ['1-3', '7-9', '13-15'].filter(b => offer[b]).map(b => b + '局 ' + pct(offer[b].Bronze, offer[b].n) + '/' + pct(offer[b].Rare, offer[b].n) + '/' + pct(offer[b].SuperRare, offer[b].n)).join('  '));
}
