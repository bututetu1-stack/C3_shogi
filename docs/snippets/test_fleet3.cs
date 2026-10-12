// 艦隊の第三回（Gemini）の仕組みを確かめる（プレイ中に流す。物鉄を成らせたあと play_mode_step で数秒進めてから test_fleet3_check.cs を流す）
// 1回目: 盤を作って物鉄を成らせる（着任の演出が進む）。対潜・基地航空隊の確かめもここでする
var gm = GameManager.Instance; var bm = BoardManager.Instance;
var sel = UnityEngine.Object.FindFirstObjectByType<PieceSelectionUI>(); if (sel != null) sel.Hide();
var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>(); if (title != null) UnityEngine.Object.Destroy(title.gameObject);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
gm.SimBeginRun();
StageManager.Instance.currentStage = 6;
typeof(GameManager).GetMethod("SetupStageBoard", bf).Invoke(gm, null);
gm.SimApplyOption(DraftOption.Piece(bm.GetPieceDataByType(PieceType.Monotetsu)));
typeof(GameManager).GetMethod("StartBattle", bf).Invoke(gm, null);

var sb = new System.Text.StringBuilder();
int size = bm.CurrentBoardSize;

// 対潜: 敵の歩兵の攻撃は潜水艦に当たり、金将の攻撃は当たらない
Vector2Int? spot = null;
for (int y = 3; y < size - 1 && spot == null; y++)
    for (int x = 0; x < size; x++)
        if (bm.IsEmpty(new Vector2Int(x, y)) && bm.IsEmpty(new Vector2Int(x, y + 1))) { spot = new Vector2Int(x, y); break; }
var ss = bm.Spawn(bm.GetPieceDataByType(PieceType.KanmusuSS), Team.Player, spot.Value);
var pawn = bm.Spawn(bm.GetPieceDataByType(PieceType.Pawn), Team.Enemy, spot.Value + Vector2Int.up);
var gold = bm.GetTeamPieces(Team.Enemy).FirstOrDefault(p => p.data.pieceType == PieceType.Gold) ?? pawn;
sb.Append("対潜: 歩兵→潜水 " + CombatResolver.CalcDamage(pawn, ss) + "、" + gold.data.displayName + "→潜水 " + (gold == pawn ? "（金将なし）" : CombatResolver.CalcDamage(gold, ss).ToString()) + "\n");
CombatResolver.ApplyDamage(ss, 999, true);   // 片付ける
CombatResolver.ApplyDamage(pawn, 999, true);

// 基地航空隊: 敵3体の体力が減る（演出は着弾を待つので、check のほうで見る）
var before = bm.GetTeamPieces(Team.Enemy).Where(p => p.data.pieceType != PieceType.C3).Sum(p => p.currentHP);
gm.StartCoroutine(AbilitySystem.Instance.LandBaseRaid());
typeof(GameManager).GetField("bonusUpgradeStage", bf).SetValue(gm, 0);
sb.Append("基地航空隊の前: 敵の体力の合計 " + before + "\n");

// 物鉄を成らせる（着任 → 出撃 → 陣形 → 制空権）
var mono = bm.GetTeamPieces(Team.Player).First(p => p.data.pieceType == PieceType.Monotetsu);
mono.Promote();
gm.StartCoroutine(AbilitySystem.Instance.TeitokuArrivalRoutine(mono));
return sb.ToString();
