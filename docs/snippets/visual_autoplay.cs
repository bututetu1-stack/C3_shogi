// 演出つきで AI どうしに1局指させる（プレイ中に流す。デバッグ用: 演出の側の例外を探す）。
// 第十局の盤で、物鉄（練度★3）・潜水艦の出やすいように仲間を入れて、物鉄をすぐ成らせる（着任 → 陣形 → 制空権 → 開幕航空戦・開幕雷撃）。
// 自軍は SimpleAI が指し、敵はいつもどおり SimpleAI が指す。このあと play_mode_step --seconds 90 などで進め、
// console_read_logs --type error で例外を見る。進み具合は GameManager.MoveCount で見る
var gm = GameManager.Instance; var bm = BoardManager.Instance;
var sel = UnityEngine.Object.FindFirstObjectByType<PieceSelectionUI>(); if (sel != null) sel.Hide();
var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>(); if (title != null) UnityEngine.Object.Destroy(title.gameObject);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
gm.SimBeginRun();
StageManager.Instance.currentStage = 10;
typeof(GameManager).GetMethod("SetupStageBoard", bf).Invoke(gm, null);
foreach (var t in new[] { PieceType.Monotetsu, PieceType.Konishiki, PieceType.Niko, PieceType.Yuu })
    gm.SimApplyOption(DraftOption.Piece(bm.GetPieceDataByType(t)));
var member = gm.Roster.Get(PieceType.Monotetsu);
if (member != null) gm.Roster.AddXp(member, 999);   // ★3（艦娘が改で出撃し、潜水艦なら開幕雷撃）
typeof(GameManager).GetMethod("StartBattle", bf).Invoke(gm, null);

var ai = SimpleAI.Instance;
System.Collections.IEnumerator Loop()
{
    // 対局が始まるのを待ってから、物鉄を成らせる
    while (gm.currentPhase != GamePhase.Battle) yield return null;
    var mono = bm.GetTeamPieces(Team.Player).FirstOrDefault(p => p.data.pieceType == PieceType.Monotetsu);
    if (mono != null) yield return gm.PromoteRoutine(mono);
    int turns = 0;
    while (turns < 30 && gm.currentPhase == GamePhase.Battle)
    {
        if (gm.currentTurn == Team.Player && !gm.IsTurnProcessing)
        {
            PieceInstance pc; MoveValidator.MoveResult mv;
            if (ai.TryChooseMove(Team.Player, 150f, out pc, out mv)) yield return CombatResolver.ExecuteMove(pc, mv);
            PieceInstance again = gm.TakeBonusMove();
            if (again != null && ai.TryChooseMove(Team.Player, 150f, out pc, out mv, again)) yield return CombatResolver.ExecuteMove(pc, mv);
            gm.EndTurn();
            turns++;
        }
        yield return null;
    }
    Debug.Log("[visual_autoplay] 終わり: 自軍の手番 " + turns + "、手数 " + gm.MoveCount + "、局面 " + gm.currentPhase);
}
gm.StartCoroutine(Loop());
return "始めた（第十局、物鉄★3）";
