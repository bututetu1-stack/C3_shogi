const fs = require('fs');
const dir = (process.argv[3] || 'BalanceReports/exp') + '/';
const load = f => {
  if (!fs.existsSync(dir + f)) return [];
  const m = new Map();   // 再開で同じ種が重なったら最初の記録を使う
  fs.readFileSync(dir + f, 'utf8').split('\n').filter(Boolean).map(JSON.parse).forEach(r => { if (!m.has(r.seed)) m.set(r.seed, r); });
  return [...m.values()];
};
const isWin = x => x.outcome === 'Won' || x.outcome === 'JudgedWin';
const wonStages = r => r.stages.filter(isWin).length;
const base = load('base.jsonl');
const baseBySeed = new Map(base.map(r => [r.seed, r]));
const mean = a => a.reduce((s, v) => s + v, 0) / Math.max(1, a.length);
const se = a => { const m = mean(a); return Math.sqrt(a.reduce((s, v) => s + (v - m) ** 2, 0) / Math.max(1, a.length - 1) / Math.max(1, a.length)); };
const out = { base: { n: base.length, avgWon: mean(base.map(wonStages)), cleared: base.filter(r => r.cleared).length,
  avgWonFirst100: mean(base.filter(r => r.seed <= 100).map(wonStages)) } };
const variants = ['Monin','Boku','Wotsu','Nako','Rihaku','SN','Konishiki','Monotetsu','Teitoku','Lance','Knight','Silver','Gold','Bishop','Rook'];
const nameOf = { Monin:'門人', Boku:'僕', Wotsu:'ヲツ', Nako:'なこ', Rihaku:'李白', SN:'SN', Konishiki:'小錦', Monotetsu:'物鉄', Teitoku:'物鉄', Lance:'香車', Knight:'桂馬', Silver:'銀将', Gold:'金将', Bishop:'角行', Rook:'飛車' };
out.variants = [];
for (const v of variants) {
  const runs = load('force_' + v + '.jsonl');
  if (!runs.length) continue;
  const diffs = runs.filter(r => baseBySeed.has(r.seed)).map(r => wonStages(r) - wonStages(baseBySeed.get(r.seed)));
  const nm = nameOf[v];
  const stages = runs.flatMap(r => r.stages);
  const app = stages.filter(x => (x.roster || []).includes(nm)).length;
  const kills = stages.reduce((a, x) => a + (x.kills || []).filter(k => k === nm).length, 0);
  const lost = stages.reduce((a, x) => a + (x.losses || []).filter(k => k === nm).length, 0);
  const promos = stages.reduce((a, x) => a + (x.promotions || []).filter(k => k === nm).length, 0);
  const kanmusuKills = stages.reduce((a, x) => a + (x.kills || []).filter(k => k === '艦娘').length, 0);
  const st = {};
  for (let s = 1; s <= 15; s++) { const r = stages.filter(x => x.stage === s); if (r.length) st[s] = [r.filter(isWin).length, r.length]; }
  out.variants.push({ v, name: nm, n: runs.length, avgWon: mean(runs.map(wonStages)), diff: mean(diffs), diffSE: se(diffs), cleared: runs.filter(r => r.cleared).length,
    killsPer: app ? kills / app : 0, lostRate: app ? lost / app : 0, promoRate: app ? promos / app : 0, kanmusuKillsPerStage: kanmusuKills / Math.max(1, stages.length), stageWin: st });
}
// 敵の分析（base）
const counts = {
 1:{歩兵:3,金将:1}, 2:{歩兵:5,金将:2,銀将:1}, 3:{歩兵:5,金将:2,銀将:2,角行:1}, 4:{歩兵:7,金将:2,銀将:2,飛車:1,角行:1},
 5:{歩兵:9,金将:2,銀将:2,桂馬:2,香車:2,飛車:1,角行:1}, 6:{歩兵:9,金将:2,銀将:2,鬼将:2,香車:2,飛車:1,角行:1},
 7:{歩兵:9,金将:2,銀将:2,鬼将:1,桂馬:1,影忍:2,飛車:1,角行:1}, 8:{歩兵:9,金将:2,銀将:2,鬼将:2,香車:2,飛車:1,角行:1,鉄壁:1},
 9:{歩兵:9,金将:2,銀将:2,天狗:2,影忍:2,飛車:1,角行:1,鉄壁:1}, 10:{歩兵:9,金将:2,鉄壁:2,鬼将:2,影忍:2,雷帝:1,角行:1,天狗:2},
 11:{歩兵:9,香車:2,風神:2,銀将:2,金将:2,髑髏:4,飛車:1,角行:1}, 12:{歩兵:9,影忍:2,鬼将:2,鉄壁:2,金将:2,閻魔:2,龍神:1,角行:1},
 13:{歩兵:9,天狗:2,鬼将:2,軍将:2,金将:2,髑髏:2,黄泉:2,雷帝:1,影忍:1}, 14:{歩兵:9,影忍:2,風神:2,閻魔:2,鉄壁:2,天狗:2,軍将:2,龍神:1,雷帝:1},
 15:{髑髏:8,龍神:2,鬼将:2,鉄壁:2,閻魔:2,軍将:2,風神:2,魔王:1,雷帝:1} };
const enemy = {};
const bstages = base.flatMap(r => r.stages);
for (const x of bstages) {
  for (const [k, c] of Object.entries(counts[x.stage] || {})) { enemy[k] = enemy[k] || { app: 0, killedPlayers: 0, died: 0, c3kills: 0 }; enemy[k].app += c; }
  (x.lossBy || []).forEach((k, i) => { if (!enemy[k]) enemy[k] = { app: 0, killedPlayers: 0, died: 0, c3kills: 0 }; enemy[k].killedPlayers++; if (x.losses[i] === 'C3') enemy[k].c3kills++; });
  (x.victims || []).forEach(k => { if (!enemy[k]) enemy[k] = { app: 0, killedPlayers: 0, died: 0, c3kills: 0 }; enemy[k].died++; });
}
out.enemy = Object.entries(enemy).map(([k, e]) => ({ name: k, app: e.app, killsPer: e.app ? e.killedPlayers / e.app : null, c3kills: e.c3kills, diedRate: e.app ? e.died / e.app : null, killed: e.killedPlayers })).sort((a, b) => (b.killsPer || 0) - (a.killsPer || 0));
// base での自軍の駒（観察）
const baseStageWin = {}; for (let s = 1; s <= 15; s++) { const r = bstages.filter(x => x.stage === s); if (r.length) baseStageWin[s] = [r.filter(isWin).length, r.length]; }
out.base.stageWin = baseStageWin;
fs.writeFileSync(process.argv[2] || 'agg3.json', JSON.stringify(out, null, 1));
console.log('base', JSON.stringify(out.base));
out.variants.forEach(v => console.log(v.v.padEnd(10), 'n', v.n, 'won', v.avgWon.toFixed(2), 'diff', (v.diff >= 0 ? '+' : '') + v.diff.toFixed(2), '±', v.diffSE.toFixed(2), 'clr', v.cleared, 'kills/局', v.killsPer.toFixed(2), 'lost', (100 * v.lostRate).toFixed(0) + '%', 'promo', (100 * v.promoRate).toFixed(0) + '%', 'kanmusuK/局', v.kanmusuKillsPerStage.toFixed(2)));
out.enemy.forEach(e => console.log(e.name.padEnd(4), 'app', e.app, 'kills/app', e.killsPer == null ? '-' : e.killsPer.toFixed(3), 'C3kills', e.c3kills, 'died', e.diedRate == null ? '-' : (100 * e.diedRate).toFixed(0) + '%'));
