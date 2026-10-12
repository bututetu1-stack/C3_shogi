// 生還した物鉄・改のケッコンカッコカリと、盤の外からの攻撃が覚醒前のボスを狙わないことを確かめる（プレイ中に流す）
var gm = GameManager.Instance; var bm = BoardManager.Instance;
var sel = UnityEngine.Object.FindFirstObjectByType<PieceSelectionUI>(); if (sel != null) sel.Hide();
var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>(); if (title != null) UnityEngine.Object.Destroy(title.gameObject);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
gm.SimBeginRun();
StageManager.Instance.currentStage = 10;   // 雷帝の局
typeof(GameManager).GetMethod("SetupStageBoard", bf).Invoke(gm, null);
gm.SimApplyOption(DraftOption.Piece(bm.GetPieceDataByType(PieceType.Monotetsu)));
typeof(GameManager).GetMethod("StartBattle", bf).Invoke(gm, null);
var sb = new System.Text.StringBuilder();

// 盤の外からの攻撃の的
var isTarget = typeof(AbilitySystem).GetMethod("IsRandomFleetTarget", bf);
var raitei = bm.GetTeamPieces(Team.Enemy).FirstOrDefault(p => p.data.pieceType == PieceType.Raitei);
var pawn = bm.GetTeamPieces(Team.Enemy).FirstOrDefault(p => p.data.pieceType == PieceType.Pawn);
sb.Append("覚醒前の雷帝を狙う: " + isTarget.Invoke(null, new object[] { raitei }) + "（false が正しい）、歩兵を狙う: " + isTarget.Invoke(null, new object[] { pawn }) + "\n");

// ケッコンカッコカリ: 生還した物鉄・改を敵陣の手前に置き、敵陣へ入れる
var mono = bm.GetTeamPieces(Team.Player).First(p => p.data.pieceType == PieceType.Monotetsu);
mono.promotionSpent = true;
int size = bm.CurrentBoardSize;
int rows = BoardCell.GetPromoteRows(size);
Vector2Int from = new Vector2Int(-1, -1), to = new Vector2Int(-1, -1);
for (int x = 0; x < size && from.x < 0; x++)
{
    var a = new Vector2Int(x, size - rows - 1); var b = new Vector2Int(x, size - rows);
    var pa = bm.GetPieceAt(a); var pb = bm.GetPieceAt(b);
    if ((pa != null && pa.data.pieceType == PieceType.C3) || (pb != null && pb.data.pieceType == PieceType.C3)) continue;
    // 敵陣の手前と敵陣の1マスを空ける
    if (pa != null && pa != mono) CombatResolver.ApplyDamage(pa, 999, true);
    if (pb != null) CombatResolver.ApplyDamage(pb, 999, true);
    if ((bm.IsEmpty(a) || bm.GetPieceAt(a) == mono) && bm.IsEmpty(b)) { from = a; to = b; }
}
if (from.x < 0) return sb.Append("敵陣の手前に空きがない").ToString();
bm.MovePiece(mono.boardPosition, from);
mono.boardPosition = from;
int hp0 = mono.MaxHP;
mono.currentHP = 1;
var move = new MoveValidator.MoveResult { position = to, isAttack = false };
gm.StartCoroutine(CombatResolver.ExecuteMove(mono, move));
sb.Append("物鉄・改 " + from + "→" + to + " 生還した姿 " + mono.IsVeteran + "\n");
sb.Append("（数秒進めてから: ringed・体力・最大体力を見る。前の最大体力 " + hp0 + "）\n");
return sb.ToString();
