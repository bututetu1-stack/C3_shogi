// AI の重い所を測る（プレイ中に流す）。第十局の盤で、評価・手の生成・手を指して戻す、を1万回ずつ回した時間と、1手を選ぶ時間を返す
var gm = GameManager.Instance; var bm = BoardManager.Instance;
var sel = UnityEngine.Object.FindFirstObjectByType<PieceSelectionUI>(); if (sel != null) sel.Hide();
var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>(); if (title != null) UnityEngine.Object.Destroy(title.gameObject);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
gm.SimBeginRun();
StageManager.Instance.currentStage = 10;
typeof(GameManager).GetMethod("SetupStageBoard", bf).Invoke(gm, null);
foreach (var t in new[] { PieceType.Wotsu, PieceType.Nako, PieceType.Konishiki, PieceType.Yuu })
    gm.SimApplyOption(DraftOption.Piece(bm.GetPieceDataByType(t)));
typeof(GameManager).GetMethod("StartBattle", bf).Invoke(gm, null);

var ai = SimpleAI.Instance;
var sb = new System.Text.StringBuilder();
int N = 10000;
var sw = System.Diagnostics.Stopwatch.StartNew();
var eval = typeof(SimpleAI).GetMethod("Evaluate", bf);
var evalArgs = new object[] { Team.Enemy };
// リフレクションの重さを引くため、デリゲートにする
var evalFn = (System.Func<Team, int>)System.Delegate.CreateDelegate(typeof(System.Func<Team, int>), ai, eval);
sw.Restart(); for (int i = 0; i < N; i++) evalFn(Team.Enemy); double evalUs = sw.Elapsed.TotalMilliseconds * 1000 / N;

var moves = new System.Collections.Generic.List<MoveValidator.MoveResult>();
var pieces = bm.GetTeamPieces(Team.Enemy);
sw.Restart();
for (int i = 0; i < N; i++) foreach (var p in pieces) MoveValidator.GetValidMoves(p, moves);
double genUs = sw.Elapsed.TotalMilliseconds * 1000 / N;

// 1手を選ぶ（自軍、工夫あり／なし）
string Pick(int features)
{
    BalanceTuning.AiFeaturesPlayer = features;
    PieceInstance pc; MoveValidator.MoveResult mv;
    var t = System.Diagnostics.Stopwatch.StartNew();
    ai.TryChooseMove(Team.Player, 150f, out pc, out mv);
    return "工夫" + features + ": 深さ" + ai.LastCompletedDepth + " " + ai.LastNodes + "局面 " + t.ElapsedMilliseconds + "ms → " + pc.data.displayName + mv.position;
}
sb.Append("盤" + bm.CurrentBoardSize + " 敵" + pieces.Count + "体 自軍" + bm.GetTeamPieces(Team.Player).Count + "体\n");
sb.Append("評価1回 " + evalUs.ToString("F1") + "µs、敵全員の手の生成 " + genUs.ToString("F1") + "µs\n");
sb.Append(Pick(0) + "\n" + Pick(1) + "\n" + Pick(3) + "\n" + Pick(7) + "\n" + Pick(15) + "\n");
BalanceTuning.AiFeaturesPlayer = 0;
return sb.ToString();
