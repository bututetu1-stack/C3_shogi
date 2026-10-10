// バランステストの自動プレイを、自動プレイ用ビルド（Builds/Sim/C3Sim.exe）で並列に回す。
// エディタは使わないので、回している間も Unity で作業できる。
//
// 準備: Unity のメニュー「C3将棋 > 自動プレイ用のビルドを作る」（コードや駒のデータを変えたら作り直す）
//
// 使い方（プロジェクトのフォルダで）:
//   node docs/balance/run_parallel.js                   速報: 通常の周を40周
//   node docs/balance/run_parallel.js --full            本番: 通常300周 + 駒ごとに「第一局で必ず仲間にする」100周
//   node docs/balance/run_parallel.js --jobs base,force_Monin --base 100 --piece-runs 60
//   node docs/balance/run_parallel.js --out BalanceReports/candA --set EnemyAtkDivisor=6 --set TwoPickStages=5
//   node docs/balance/run_parallel.js --out BalanceReports/pre --preset pre   第1版より前の設定（第十四局の配置だけは今のまま）
// オプション:
//   --workers N   同時に動かす数（既定 8）  --chunk N   1本が回す周（既定 20）
//   --set 名前=値 BalanceTuning を変える    --piece 駒=JSON   PieceData の一部を上書き（例: C3={"baseHP":10}）
// 結果は <out>/<名前>.jsonl に1周1行で追記する。止めてもう一度実行すれば、足りない種だけ回す。
// 集計: node docs/balance/aggregate.js 出力.json <out>
const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');

const root = path.resolve(__dirname, '..', '..');
const exe = path.join(root, 'Builds', 'Sim', 'C3Sim.exe');

// ---- 引数 ----
const argv = process.argv.slice(2);
const opt = { workers: 8, chunk: 20, base: 40, pieceRuns: 100, out: 'BalanceReports/exp', jobs: null, set: [], piece: [], preset: null, full: false };
for (let i = 0; i < argv.length; i++) {
  const a = argv[i], v = argv[i + 1];
  if (a === '--workers') { opt.workers = +v; i++; }
  else if (a === '--chunk') { opt.chunk = +v; i++; }
  else if (a === '--base') { opt.base = +v; i++; }
  else if (a === '--piece-runs') { opt.pieceRuns = +v; i++; }
  else if (a === '--out') { opt.out = v; i++; }
  else if (a === '--jobs') { opt.jobs = v.split(','); i++; }
  else if (a === '--set') { opt.set.push(v); i++; }
  else if (a === '--piece') { opt.piece.push(v); i++; }
  else if (a === '--preset') { opt.preset = v; i++; }
  else if (a === '--full') { opt.full = true; }
  else { console.error('知らないオプション: ' + a); process.exit(2); }
}
if (opt.full && !argv.includes('--base')) opt.base = 300;

// 第1版より前（2026-10-10 の基準と同じ設定）。第十四局の雷帝→鬼将はコードなので戻らない
const dir = (x, y, d) => ({ direction: { x, y }, maxDistance: d, canJump: false });
const PRESETS = {
  pre: {
    set: ['EnemyAtkDivisor=5', 'TwoPickStages=', 'RecruitBonusHP=0', 'RecruitBonusATK=0', 'WotsuChukaCount=1', 'ChukaNearAllies=false',
      'ChukaHeal=1', 'AirRaidSplashTargets=3', 'BombardmentSplashDamage=2'],
    piece: [
      'C3=' + JSON.stringify({ baseHP: 10 }),
      'Kanmusu=' + JSON.stringify({ baseATK: 5 }),
      'Konishiki=' + JSON.stringify({ baseDEF: 1, baseHP: 8, promotedDEF: 1, promotedHP: 8 }),
      'Monin=' + JSON.stringify({ promotedMoveDirections: [[0, 1], [0, -1], [1, 0], [-1, 0], [1, 1], [-1, 1], [1, -1], [-1, -1]].map(([x, y]) => dir(x, y, 9)) }),
      'Monotetsu=' + JSON.stringify({ baseHP: 1, moveDirections: [dir(0, 1, 1), dir(1, 0, 1), dir(-1, 0, 1)] }),
    ],
  },
};
if (opt.preset) {
  const p = PRESETS[opt.preset];
  if (!p) { console.error('知らないプリセット: ' + opt.preset); process.exit(2); }
  opt.set = p.set.concat(opt.set);
  opt.piece = p.piece.concat(opt.piece);
}

// ---- 実験の一覧（run_experiments.ps1 と同じ名前） ----
const PIECES = ['Monin', 'Boku', 'Wotsu', 'Nako', 'Rihaku', 'SN', 'Konishiki', 'Monotetsu', 'Lance', 'Knight', 'Silver', 'Gold', 'Bishop', 'Rook'];
let jobs = [{ name: 'base', runs: opt.base, args: [] }];
for (const p of PIECES) jobs.push({ name: 'force_' + p, runs: opt.pieceRuns, args: ['-force', p] });
jobs.push({ name: 'force_Teitoku', runs: opt.pieceRuns, args: ['-force', 'Monotetsu', '-teitoku'] });
if (opt.jobs) jobs = jobs.filter(j => opt.jobs.includes(j.name));
else if (!opt.full) jobs = jobs.filter(j => j.name === 'base');

// ---- 準備 ----
if (!fs.existsSync(exe)) { console.error('ビルドがありません: ' + exe + '\nUnity のメニュー「C3将棋 > 自動プレイ用のビルドを作る」で作ってください'); process.exit(1); }
const newest = d => {
  let t = 0;
  if (!fs.existsSync(d)) return t;
  for (const e of fs.readdirSync(d, { withFileTypes: true })) {
    const f = path.join(d, e.name);
    t = Math.max(t, e.isDirectory() ? newest(f) : fs.statSync(f).mtimeMs);
  }
  return t;
};
const built = fs.statSync(path.join(root, 'Builds', 'Sim', 'C3Sim_Data')).mtimeMs;
if (['Assets/Scripts', 'Assets/Data', 'Assets/ScriptableObjects'].some(d => newest(path.join(root, d)) > built))
  console.warn('注意: ビルドのあとにコードか駒のデータが変わっています。作り直さないと古い内容で回ります');

const outDir = path.resolve(root, opt.out);
const tmpDir = path.join(outDir, '.tmp');
fs.mkdirSync(tmpDir, { recursive: true });
const doneSeeds = name => {
  const f = path.join(outDir, name + '.jsonl');
  if (!fs.existsSync(f)) return new Set();
  return new Set(fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l).seed));
};

// 足りない種を、続きの番号ごとに chunk 周ずつの仕事に分ける
const tasks = [];
for (const j of jobs) {
  const have = doneSeeds(j.name);
  let start = null, count = 0;
  const flush = () => { if (start != null) tasks.push({ job: j, seed: start, runs: count }); start = null; count = 0; };
  for (let s = 1; s <= j.runs; s++) {
    if (have.has(s)) { flush(); continue; }
    if (start == null) start = s;
    count++;
    if (count >= opt.chunk) flush();
  }
  flush();
}
const totalRuns = tasks.reduce((a, t) => a + t.runs, 0);
if (!tasks.length) { console.log('回す周はありません（すべて記録済み）: ' + outDir); process.exit(0); }
console.log(`${jobs.map(j => j.name).join(', ')} を ${totalRuns}周（${tasks.length}本、同時に${opt.workers}本）→ ${outDir}`);
if (opt.set.length || opt.piece.length) console.log('設定: ' + opt.set.concat(opt.piece).join('  '));

// ---- 並列に回す ----
const extra = [];
opt.set.forEach(s => extra.push('-set', s));
opt.piece.forEach(p => extra.push('-piece', p));
const t0 = Date.now();
let next = 0, finished = 0, runsDone = 0, failed = 0;

function runTask(t) {
  return new Promise(resolve => {
    const base = `${t.job.name}_${t.seed}`;
    const tmp = path.join(tmpDir, base + '.jsonl');
    const log = path.join(tmpDir, base + '.log');
    if (fs.existsSync(tmp)) fs.unlinkSync(tmp);
    const args = ['-batchmode', '-nographics', '-logFile', log, '-c3sim', '-runs', String(t.runs), '-seed', String(t.seed), '-out', tmp, ...t.job.args, ...extra];
    const started = Date.now();
    const child = spawn(exe, args, { stdio: 'ignore', windowsHide: true });
    child.on('exit', code => {
      finished++;
      const lines = fs.existsSync(tmp) ? fs.readFileSync(tmp, 'utf8') : '';
      const n = lines.split('\n').filter(Boolean).length;
      if (code === 0 && n === t.runs) {
        fs.appendFileSync(path.join(outDir, t.job.name + '.jsonl'), lines);
        fs.unlinkSync(tmp);
        if (fs.existsSync(log)) fs.unlinkSync(log);
        runsDone += n;
      } else {
        failed++;
        console.error(`失敗: ${t.job.name} ${t.seed}〜（終了コード ${code}、${n}/${t.runs}周）ログ: ${log}`);
      }
      const el = (Date.now() - t0) / 1000;
      const left = runsDone > 0 ? Math.round(el / runsDone * (totalRuns - runsDone) / 60) : '?';
      console.log(`[${finished}/${tasks.length}] ${t.job.name} ${t.seed}〜${t.seed + t.runs - 1} ${code === 0 ? 'ok' : 'NG'}（${Math.round((Date.now() - started) / 1000)}秒）残り約${left}分`);
      resolve();
    });
  });
}

async function worker() {
  while (next < tasks.length) await runTask(tasks[next++]);
}

Promise.all(Array.from({ length: Math.min(opt.workers, tasks.length) }, worker)).then(() => {
  const min = ((Date.now() - t0) / 60000).toFixed(1);
  console.log(`${failed ? '一部失敗' : 'ALL DONE'}: ${runsDone}周、${min}分`);
  try { fs.rmdirSync(tmpDir); } catch (e) { /* 失敗したログが残っていれば消さない */ }
  process.exit(failed ? 1 : 0);
});
