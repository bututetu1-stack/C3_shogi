// 盤の外からの攻撃が覚醒前のボスを狙わないことを確かめる（プレイ中に流す）。ケッコンカッコカリは test_ring.cs
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

return sb.ToString();
