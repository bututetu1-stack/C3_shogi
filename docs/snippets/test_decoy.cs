// 潜水艦の潜航（通り抜け・くぐる動き・組み止めなし）と、挑発の引きつけを確かめる（プレイ中に流す）
var gm = GameManager.Instance; var bm = BoardManager.Instance;
var sel = UnityEngine.Object.FindFirstObjectByType<PieceSelectionUI>(); if (sel != null) sel.Hide();
var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>(); if (title != null) UnityEngine.Object.Destroy(title.gameObject);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
gm.SimBeginRun();
StageManager.Instance.currentStage = 6;
typeof(GameManager).GetMethod("SetupStageBoard", bf).Invoke(gm, null);
gm.SimApplyOption(DraftOption.Piece(bm.GetPieceDataByType(PieceType.Konishiki)));
typeof(GameManager).GetMethod("StartBattle", bf).Invoke(gm, null);

var sb = new System.Text.StringBuilder();
int size = bm.CurrentBoardSize;
System.Func<int, int, bool> free = (x, y) => x >= 0 && y >= 0 && x < size && y < size && bm.IsEmpty(new Vector2Int(x, y));

// 真ん中の列の5マス（C3 のない列）を空けて、下から 抜けた先2 / 抜けた先1 / 潜水 / 猪突 / くぐった先 に使う
int cy = size / 2, cx = -1;
for (int x = size / 2; x < size && cx < 0; x++)
{
    bool hasC3 = false;
    for (int y = cy - 2; y <= cy + 2; y++) { var p = bm.GetPieceAt(new Vector2Int(x, y)); if (p != null && p.data.pieceType == PieceType.C3) hasC3 = true; }
    if (!hasC3) cx = x;
}
for (int y = cy - 2; y <= cy + 2; y++) { var p = bm.GetPieceAt(new Vector2Int(cx, y)); if (p != null) CombatResolver.ApplyDamage(p, 999, true); }
if (!free(cx, cy - 2) || !free(cx, cy + 2)) return "列を空けられない";
var ss = bm.Spawn(bm.GetPieceDataByType(PieceType.KanmusuSS), Team.Player, new Vector2Int(cx, cy));
var boar = bm.Spawn(bm.GetPieceDataByType(PieceType.Chototsu), Team.Enemy, new Vector2Int(cx, cy + 1));
sb.Append("盤" + size + " 潜水" + ss.boardPosition + " 猪突" + boar.boardPosition + "\n");

var boarMoves = MoveValidator.GetValidMoves(boar);
sb.Append("猪突は潜水の上を抜けて進める: " + boarMoves.Exists(m => m.position == new Vector2Int(cx, cy - 1) && !m.isAttack) + "\n");
sb.Append("猪突は潜水から離れられる（組み止めなし）: " + boarMoves.Exists(m => m.position == new Vector2Int(cx, cy - 2)) + "\n");
sb.Append("猪突の攻撃先: " + string.Join(",", boarMoves.Where(m => m.isAttack).Select(m => bm.GetPieceAt(m.position).data.displayName)) + "\n");

var ssMoves = MoveValidator.GetValidMoves(ss);
sb.Append("潜水は猪突の下をくぐって2マス先へ: " + ssMoves.Exists(m => m.position == new Vector2Int(cx, cy + 2) && !m.isAttack) + "（動ける先 " + ssMoves.Count + "）\n");
sb.Append("潜水への通常の攻撃: " + CombatResolver.CalcDamage(boar, ss) + "\n");

// 引きつけ: 小錦の2マス先にいる敵は、小錦に近づく手に点が付く
var ai = UnityEngine.Object.FindFirstObjectByType<SimpleAI>();
var ko = bm.GetTeamPieces(Team.Player).First(p => p.data.pieceType == PieceType.Konishiki);
typeof(SimpleAI).GetMethod("CollectLures", bf).Invoke(ai, new object[] { Team.Player, true });
var lure = typeof(SimpleAI).GetMethod("LureBonus", bf);
PieceInstance near = null;
foreach (var e in bm.GetTeamPieces(Team.Enemy))
    if (e.data.pieceType != PieceType.C3 && Mathf.Max(Mathf.Abs(e.boardPosition.x - ko.boardPosition.x), Mathf.Abs(e.boardPosition.y - ko.boardPosition.y)) <= 2) { near = e; break; }
if (near == null)
{
    // 小錦からちょうど2マスの空きマスに敵の妖狐（全方向1）を置く
    for (int dx = -2; dx <= 2 && near == null; dx++)
        for (int dy = -2; dy <= 2 && near == null; dy++)
            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) == 2 && free(ko.boardPosition.x + dx, ko.boardPosition.y + dy))
                near = bm.Spawn(bm.GetPieceDataByType(PieceType.Youko), Team.Enemy, new Vector2Int(ko.boardPosition.x + dx, ko.boardPosition.y + dy));
}
if (near == null) sb.Append("小錦" + ko.boardPosition + " の2マス先に敵を置けない\n");
else
{
    var parts = new System.Collections.Generic.List<string>();
    foreach (var m in MoveValidator.GetValidMoves(near))
        parts.Add(m.position + (m.isAttack ? "攻" : "") + "=" + lure.Invoke(ai, new object[] { near, m }));
    sb.Append("小錦" + ko.boardPosition + " の近くの " + near.data.displayName + near.boardPosition + " の手と引きつけの点: " + string.Join(" ", parts) + "\n");
}
return sb.ToString();
