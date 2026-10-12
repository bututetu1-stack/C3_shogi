// 部員ごとの強さの表（2026-10-12）。通常の周の観察（盤にいた局の撃破・味方の撃破に占める割合・成る率・覚醒する率・倒される率）と、
// 第一局から持たせた周（force_X）・覚醒させた周（awaken_X）の比べ（base との到達局の差、序盤の勝つまでの手数の比＝基準1）。
// 全クリアへの差は揺れ（±0.6局ほど）に埋もれやすいので、撃破の割合と序盤の手数の比で「局の中での強さ」を見る。
// 使い方: node docs/balance/member_table.js BalanceReports/fb11   （--json で JSON を出す）
const fs = require('fs');
const dir = process.argv[2] || 'BalanceReports/fb11';
// 記録に出る名前（駒の名前）
const members = { Monin: '門人', Boku: '僕', Wotsu: 'ヲツ', Nako: 'なこ', Rihaku: '李白', SN: 'SN', Konishiki: '小錦', Kei: '鶏炭', Yuu: 'ユウ', Kipu: 'きぷ',
  Monotetsu: '物鉄', Mitsuharu: '光春', Niko: 'ニコ', Shimesaba: '〆鯖', Kawasemi: '翡翠', RN: 'RN', Negu: '寝癖', Mutou: '無糖' };
const load = f => {
  const p = dir + '/' + f + '.jsonl';
  if (!fs.existsSync(p)) return [];
  const m = new Map();
  for (const l of fs.readFileSync(p, 'utf8').split('\n')) { if (!l.trim()) continue; const r = JSON.parse(l); if (!m.has(r.seed)) m.set(r.seed, r); }
  return [...m.values()];
};
const isWin = x => x.outcome === 'Won' || x.outcome === 'JudgedWin';
const won = r => r.stages.filter(isWin).length;
const mean = a => a.reduce((s, v) => s + v, 0) / Math.max(1, a.length);
const se = a => { const m = mean(a); return Math.sqrt(a.reduce((s, v) => s + (v - m) ** 2, 0) / Math.max(1, a.length - 1) / Math.max(1, a.length)); };
const base = load('base');
const baseBy = new Map(base.map(r => [r.seed, r]));
const bst = base.flatMap(r => r.stages);
// 局ごとの基準の手数（勝った局）
const baseMoves = {};
for (let s = 1; s <= 15; s++) { const w = bst.filter(x => x.stage === s && isWin(x)); baseMoves[s] = mean(w.map(x => x.moves)); }
const rows = [];
for (const [t, nm] of Object.entries(members)) {
  // 通常の周での観察（盤にいた局）
  const app = bst.filter(x => (x.roster || []).includes(nm));
  const kills = app.reduce((a, x) => a + (x.kills || []).filter(k => k === nm).length, 0);
  const teamKills = app.reduce((a, x) => a + (x.kills || []).length, 0);
  const promos = app.filter(x => (x.promotions || []).includes(nm)).length;
  const awak = app.filter(x => (x.awakenings || []).some(a => a.split('@')[0] === nm)).length;
  const lost = app.reduce((a, x) => a + (x.losses || []).filter(k => k === nm).length, 0);
  const row = { type: t, name: nm,
    app: app.length, killsPer: kills / Math.max(1, app.length), killShare: kills / Math.max(1, teamKills),
    promoRate: promos / Math.max(1, app.length), awakenRate: awak / Math.max(1, app.length), lostRate: lost / Math.max(1, app.length) };
  for (const [prefix, key] of [['force_', 'force'], ['awaken_', 'awaken']]) {
    const runs = load(prefix + t);
    if (!runs.length) continue;
    const paired = runs.filter(r => baseBy.has(r.seed));
    const diffs = paired.map(r => won(r) - won(baseBy.get(r.seed)));
    const st = runs.flatMap(r => r.stages);
    // 序盤（第一〜八局）の勝つまでの手数の比（基準=1）
    const early = st.filter(x => x.stage <= 8 && isWin(x));
    const speed = mean(early.map(x => x.moves / baseMoves[x.stage]));
    const late = st.filter(x => x.stage >= 13);
    const fApp = st.filter(x => (x.roster || []).includes(nm));
    const fKills = fApp.reduce((a, x) => a + (x.kills || []).filter(k => k === nm).length, 0);
    row[key] = { n: runs.length, avgWon: mean(runs.map(won)), diff: mean(diffs), diffSE: se(diffs), clear: runs.filter(r => r.cleared).length / runs.length,
      earlyMoves: speed, lateWin: late.filter(isWin).length / Math.max(1, late.length), killsPer: fKills / Math.max(1, fApp.length) };
  }
  rows.push(row);
}
const baseLate = bst.filter(x => x.stage >= 13);
const summary = { n: base.length, avgWon: mean(base.map(won)), clear: base.filter(r => r.cleared).length / base.length, lateWin: baseLate.filter(isWin).length / baseLate.length };
if (process.argv.includes('--json')) { console.log(JSON.stringify({ summary, rows }, null, 1)); process.exit(0); }
console.log('base: ' + summary.n + '周 到達' + summary.avgWon.toFixed(2) + ' 全クリア' + (100 * summary.clear).toFixed(0) + '%');
for (const r of rows.sort((a, b) => b.killShare - a.killShare)) {
  const f = r.force, a = r.awaken;
  console.log(r.name.padEnd(4), '盤にいた局', String(r.app).padStart(4), '撃破/局', r.killsPer.toFixed(2), '割合', (100 * r.killShare).toFixed(0) + '%',
    '成る', (100 * r.promoRate).toFixed(0) + '%', '覚醒', (100 * r.awakenRate).toFixed(0) + '%', '倒され', (100 * r.lostRate).toFixed(0) + '%',
    f ? ('| 第一局から n' + f.n + ' 差' + (f.diff >= 0 ? '+' : '') + f.diff.toFixed(2) + '±' + f.diffSE.toFixed(2) + ' 序盤' + f.earlyMoves.toFixed(2)) : '',
    a ? ('| 覚醒 差' + (a.diff >= 0 ? '+' : '') + a.diff.toFixed(2) + '±' + a.diffSE.toFixed(2)) : '');
}
