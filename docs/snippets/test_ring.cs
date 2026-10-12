// ケッコンカッコカリ（改二の MVP に指輪）の確かめ（プレイ中に流す）。1) この snippet で着任させる 2) 数秒進めて test_ring_check.cs
// ※ 中断データ（続きから）を上書きすることがある
var gm = GameManager.Instance; var bm = BoardManager.Instance; var ab = AbilitySystem.Instance;
var sel = UnityEngine.Object.FindFirstObjectByType<PieceSelectionUI>(); if (sel != null) sel.Hide();
var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>(); if (title != null) UnityEngine.Object.Destroy(title.gameObject);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
gm.SimBeginRun();
StageManager.Instance.currentStage = 6;
typeof(GameManager).GetMethod("SetupStageBoard", bf).Invoke(gm, null);
gm.SimApplyOption(DraftOption.Piece(bm.GetPieceDataByType(PieceType.Monotetsu)));
typeof(GameManager).GetMethod("StartBattle", bf).Invoke(gm, null);
// すべての艦種とケッコン済みにして、提督を着任させる（出撃する2隻とも改二・指輪で出るはず）
gm.RingedClasses.Clear();
foreach (var t in new[] { PieceType.KanmusuDD, PieceType.KanmusuCL, PieceType.KanmusuCA, PieceType.KanmusuBB, PieceType.KanmusuCV, PieceType.KanmusuSS }) gm.RingedClasses.Add(t);
var mono = bm.GetTeamPieces(Team.Player).First(p => p.data.pieceType == PieceType.Monotetsu);
mono.Promote();
gm.StartCoroutine(ab.TeitokuArrivalRoutine(mono));
return "着任 " + mono.boardPosition;
