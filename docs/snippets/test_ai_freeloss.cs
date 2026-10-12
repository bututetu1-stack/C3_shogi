// 敵の AI が「ただで取られる手」をどれくらい選ぶか数える（プレイ中に流す）。局の初めの局面で、敵に何度も1手を選ばせ、
// 選んだ手が SimpleAI.LosesForFree（動いた駒が相手の次の手でただで倒される）に当たるかを数える。
// BalanceTuning.AiAvoidFreeLoss を切ったときと入れたときで比べる
var gm = GameManager.Instance; var bm = BoardManager.Instance;
var sel = UnityEngine.Object.FindFirstObjectByType<PieceSelectionUI>(); if (sel != null) sel.Hide();
var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>(); if (title != null) UnityEngine.Object.Destroy(title.gameObject);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var meType = typeof(SimpleAI).GetNestedType("MoveEntry", System.Reflection.BindingFlags.NonPublic);
var losesForFree = typeof(SimpleAI).GetMethod("LosesForFree", bf);
var ai = SimpleAI.Instance;
GameSim.Headless = true;
var sb = new System.Text.StringBuilder();
foreach (int stage in new[] { 1, 2, 3, 4 })
{
    gm.SimBeginRun();
    StageManager.Instance.currentStage = stage;
    typeof(GameManager).GetMethod("SetupStageBoard", bf).Invoke(gm, null);
    gm.SimApplyOption(DraftOption.Piece(bm.GetPieceDataByType(PieceType.Boku)));
    typeof(GameManager).GetMethod("StartBattle", bf).Invoke(gm, null);
    foreach (bool avoid in new[] { false, true })
    {
        BalanceTuning.AiAvoidFreeLoss = avoid;
        int hang = 0, total = 0;
        for (int i = 0; i < 30; i++)
        {
            PieceInstance pc; MoveValidator.MoveResult mv;
            if (!ai.TryChooseMove(Team.Enemy, 150f, out pc, out mv)) continue;
            total++;
            object e = System.Activator.CreateInstance(meType);
            meType.GetField("piece").SetValue(e, pc);
            meType.GetField("move").SetValue(e, mv);
            if ((bool)losesForFree.Invoke(ai, new object[] { e, Team.Enemy, Team.Player })) hang++;
        }
        sb.Append("第" + stage + "局の初手 ただ取られ除け " + (avoid ? "あり" : "なし") + ": ただで取られる手 " + hang + "/" + total + "\n");
    }
}
BalanceTuning.AiAvoidFreeLoss = true;
GameSim.Headless = false;
return sb.ToString();
