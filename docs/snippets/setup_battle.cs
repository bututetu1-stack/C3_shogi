var gm = GameManager.Instance; var bm = BoardManager.Instance;
var sel = UnityEngine.Object.FindFirstObjectByType<PieceSelectionUI>(); if (sel != null) sel.Hide();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
StageManager.Instance.currentStage = 6;
typeof(GameManager).GetMethod("SetupStageBoard", bf).Invoke(gm, null);
foreach (var t in new[] { PieceType.Mitsuharu, PieceType.Niko, PieceType.Shimesaba, PieceType.Kawasemi, PieceType.Wotsu })
  gm.SimApplyOption(DraftOption.Piece(bm.GetPieceDataByType(t)));
gm.SecretFund = 5;
typeof(GameManager).GetMethod("StartBattle", bf).Invoke(gm, null);
return "ok";
