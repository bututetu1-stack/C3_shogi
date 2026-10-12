// RN（成った後）の体幹と忍殺を確かめる（プレイ中に流す）。ふつうの敵は忍殺で倒れ、ボスは1/3を失って動けなくなる
var gm = GameManager.Instance; var bm = BoardManager.Instance;
var sel = UnityEngine.Object.FindFirstObjectByType<PieceSelectionUI>(); if (sel != null) sel.Hide();
var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>(); if (title != null) UnityEngine.Object.Destroy(title.gameObject);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
gm.SimBeginRun();
StageManager.Instance.currentStage = 10;   // 雷帝のいる局
typeof(GameManager).GetMethod("SetupStageBoard", bf).Invoke(gm, null);
gm.SimApplyOption(DraftOption.Piece(bm.GetPieceDataByType(PieceType.RN)));
typeof(GameManager).GetMethod("StartBattle", bf).Invoke(gm, null);

var sb = new System.Text.StringBuilder();
var rn = bm.GetTeamPieces(Team.Player).First(p => p.data.pieceType == PieceType.RN);
rn.Promote();
rn.AddMaxHP(50);
var enemies = bm.GetTeamPieces(Team.Enemy).Where(e => e.data.pieceType != PieceType.C3 && e.isAlive).ToList();

// ふつうの敵: 素のダメージを RN の防御+2 にして、ブロック3で全部防ぐ（弾き: 体幹+3）
var e1 = enemies.First(e => e.data.pieceType != PieceType.Raitei);
e1.bonusATK += (rn.DEF + 2) - e1.ATK;
sb.Append("ふつうの敵 " + e1.data.displayName + " 体力" + e1.MaxHP + ": ");
for (int i = 0; i < 10 && e1.isAlive; i++)
{
    rn.block = 3;
    CombatResolver.Attack(e1, rn);
    sb.Append("[体幹" + e1.posture + (e1.isAlive ? "" : " 忍殺で撃破") + "]");
}
sb.Append("\n");

// ボス: 素のダメージを RN の防御+3 に（ブロック3で全部防ぐ: 体幹+4）
var boss = enemies.First(e => e.data.pieceType == PieceType.Raitei);
boss.bonusATK += (rn.DEF + 3) - boss.ATK;
int hp0 = boss.currentHP;
sb.Append("雷帝 体力" + boss.currentHP + "/" + boss.MaxHP + ": ");
for (int i = 0; i < 10 && boss.isAlive; i++)
{
    rn.block = 3;
    int before = boss.currentHP;
    CombatResolver.Attack(boss, rn);
    sb.Append("[体幹" + boss.posture + " 体力" + boss.currentHP + (boss.stunned ? " 動けない" : "") + "]");
    if (boss.currentHP < before) break;
}
sb.Append("\nstunHeld " + boss.stunHeld);
return sb.ToString();
