// 盤を用意して、RN・寝癖・無糖の能力を1つずつ動かして数値を確かめる（プレイ中に流す）
var gm = GameManager.Instance; var bm = BoardManager.Instance; var ab = AbilitySystem.Instance;
var sel = UnityEngine.Object.FindFirstObjectByType<PieceSelectionUI>(); if (sel != null) sel.Hide();
var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>(); if (title != null) UnityEngine.Object.Destroy(title.gameObject);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
gm.SimBeginRun();
StageManager.Instance.currentStage = 6;
typeof(GameManager).GetMethod("SetupStageBoard", bf).Invoke(gm, null);
foreach (var t in new[] { PieceType.RN, PieceType.Negu, PieceType.Mutou, PieceType.Monotetsu })
    gm.SimApplyOption(DraftOption.Piece(bm.GetPieceDataByType(t)));
typeof(GameManager).GetMethod("StartBattle", bf).Invoke(gm, null);

var sb = new System.Text.StringBuilder();
System.Func<PieceType, PieceInstance> P = t => bm.GetTeamPieces(Team.Player).FirstOrDefault(p => p.data.pieceType == t && p.isAlive);
var rn = P(PieceType.RN); var negu = P(PieceType.Negu); var mutou = P(PieceType.Mutou);
var enemies = bm.GetTeamPieces(Team.Enemy).Where(e => e.data.pieceType != PieceType.C3 && e.isAlive).ToList();
sb.Append("敵 " + enemies.Count + "体\n");

// 1) 手番の初め: えちち連合
int a0 = negu.ATK;
GameSim.RunSync(ab.ExecuteTurnStartAbilities(Team.Player));
sb.Append("1 連合 " + negu.allianceATK + " 攻 " + a0 + "→" + negu.ATK + "\n");

// 2) 手番の終わり: ブロック・デイリー
GameSim.RunSync(ab.ExecuteTurnEndAbilities(Team.Player));
sb.Append("2 RNブロック " + rn.block + " / 寝癖デイリー " + negu.dailyCount + "\n");
GameSim.RunSync(ab.ExecuteTurnEndAbilities(Team.Player));
GameSim.RunSync(ab.ExecuteTurnEndAbilities(Team.Player));
sb.Append("2b 3手番後 寝癖デイリー " + negu.dailyCount + " 報酬 " + negu.dailyRewards + " 攻 " + negu.ATK + "\n");

// 3) ブロックで防ぐ（成る前は弾かない）
var e1 = enemies[0];
e1.bonusATK += (rn.DEF + 2) - e1.ATK;
int h0 = rn.currentHP, eh0 = e1.currentHP;
CombatResolver.Attack(e1, rn);
sb.Append("3 RN被弾(素2) 体力 " + h0 + "→" + rn.currentHP + " ブロック残り " + rn.block + " 敵 " + eh0 + "→" + e1.currentHP + "\n");

// 4) 成った RN の弾き
rn.Promote(); rn.block = 3;
var e2 = enemies[1];
e2.bonusATK += (rn.DEF + 2) - e2.ATK;
h0 = rn.currentHP; eh0 = e2.currentHP;
CombatResolver.Attack(e2, rn);
sb.Append("4 弾き RN " + h0 + "→" + rn.currentHP + " ブロック " + rn.block + " 敵 " + eh0 + "→" + e2.currentHP + "\n");
// ブロックを超える攻撃（弾かない）
e2.bonusATK += 3; rn.block = 3; h0 = rn.currentHP; eh0 = e2.currentHP;
CombatResolver.Attack(e2, rn);
sb.Append("4b 素5 RN " + h0 + "→" + rn.currentHP + " ブロック " + rn.block + " 敵 " + eh0 + "→" + e2.currentHP + "\n");

// 5) ポケモンスリープ（動かなかった手番に回復）
rn.currentHP = Mathf.Max(1, rn.MaxHP - 2); rn.acted = false; h0 = rn.currentHP;
GameSim.RunSync(ab.ExecuteTurnEndAbilities(Team.Player));
sb.Append("5 睡眠 " + h0 + "→" + rn.currentHP + " ブロック " + rn.block + "\n");

// 6) 無糖（AC）のスタッガー
mutou.Promote();
var e3 = enemies[2];
e3.AddMaxHP(30);
eh0 = e3.currentHP;
CombatResolver.Attack(mutou, e3);
int d1 = eh0 - e3.currentHP;
sb.Append("6 一撃目 " + d1 + " スタッガー " + e3.staggered + "\n");
eh0 = e3.currentHP;
CombatResolver.Attack(mutou, e3);
sb.Append("6b 二撃目 " + (eh0 - e3.currentHP) + "（+" + BalanceTuning.MutouStaggerDamage + " のはず） スタッガー " + e3.staggered + "\n");

// 7) 外跳ね（向こうが空いていれば跳ね飛ばす）
negu.Promote();
var e4 = enemies[3];
e4.AddMaxHP(30);
Vector2Int[] dirs = { new Vector2Int(0,1), new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,-1), new Vector2Int(1,1), new Vector2Int(-1,1) };
Vector2Int? side = null;
foreach (var d in dirs)
{
    var a = negu.boardPosition + d; var b = a + d;
    if (bm.IsInBounds(a) && bm.IsInBounds(b) && bm.IsEmpty(a) && bm.IsEmpty(b)) { side = d; break; }
}
if (side.HasValue)
{
    CombatResolver.MovePieceTo(e4, negu.boardPosition + side.Value);
    var before = e4.boardPosition;
    CombatResolver.Attack(negu, e4);
    sb.Append("7 外跳ね " + before + "→" + e4.boardPosition + "\n");
}
else sb.Append("7 跳ね飛ばす場所なし\n");

// 8) 無糖の死んで覚える・篝火
gm.Awaken(mutou, false);
var pos0 = mutou.boardPosition;
CombatResolver.KillPiece(mutou);
var m = gm.Roster.Get(PieceType.Mutou);
sb.Append("8 篝火 生存 " + mutou.isAlive + " " + pos0 + "→" + mutou.boardPosition + " 体力 " + mutou.currentHP + "/" + mutou.MaxHP + " 死 " + m.deaths + "\n");
CombatResolver.KillPiece(mutou);
sb.Append("8b 二度目 生存 " + mutou.isAlive + " 死 " + m.deaths + " 次の局の上乗せ " + gm.Roster.DeathBonus(PieceType.Mutou) + "\n");
sb.Append("状態 RN: " + AbilitySystem.StatusLine(rn) + " / 寝癖: " + AbilitySystem.StatusLine(negu) + " / 敵: " + AbilitySystem.StatusLine(e3));
return sb.ToString();
