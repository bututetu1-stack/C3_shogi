// えちち連合（会長の攻撃・同志の防御）を盤の上で確かめる（プレイ中に流す）
var gm = GameManager.Instance; var bm = BoardManager.Instance; var ab = AbilitySystem.Instance;
var sel = UnityEngine.Object.FindFirstObjectByType<PieceSelectionUI>(); if (sel != null) sel.Hide();
var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>(); if (title != null) UnityEngine.Object.Destroy(title.gameObject);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
gm.SimBeginRun();
StageManager.Instance.currentStage = 6;
typeof(GameManager).GetMethod("SetupStageBoard", bf).Invoke(gm, null);
foreach (var t in new[] { PieceType.RN, PieceType.Negu, PieceType.Mutou })
    gm.SimApplyOption(DraftOption.Piece(bm.GetPieceDataByType(t)));
typeof(GameManager).GetMethod("StartBattle", bf).Invoke(gm, null);

var sb = new System.Text.StringBuilder();
System.Func<PieceType, PieceInstance> P = t => bm.GetTeamPieces(Team.Player).FirstOrDefault(p => p.data.pieceType == t && p.isAlive);
var rn = P(PieceType.RN); var negu = P(PieceType.Negu); var mutou = P(PieceType.Mutou);
int size = bm.CurrentBoardSize;
System.Func<Vector2Int, bool> free = c => bm.IsInBounds(c) && bm.IsEmpty(c);
System.Action<PieceInstance, Vector2Int> put = (p, c) => { bm.RemovePieceFromBoard(p.boardPosition); bm.PlacePieceOnBoard(p, c); };
System.Func<PieceInstance, bool> lonely = p =>
{
    for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
    {
        if (dx == 0 && dy == 0) continue;
        var q = bm.GetPieceAt(p.boardPosition + new Vector2Int(dx, dy));
        if (q != null && q.team == p.team && AbilitySystem.IsAllianceMember(q.data.pieceType)) return false;
    }
    return true;
};

// 1) 会長: 仲間は RN・無糖の2人（物鉄なし）→ 攻撃+2（上限2）
int a0 = negu.ATK - negu.allianceATK;
GameSim.RunSync(ab.ExecuteTurnStartAbilities(Team.Player));
sb.Append("1 会長 仲間2人 → 連合 +" + negu.allianceATK + "（攻 " + a0 + "→" + negu.ATK + "）\n");

// 2) 同志: RN と無糖を離して置く → どちらも上がらない。隣に並べる → 両方+1
var spots = new System.Collections.Generic.List<Vector2Int>();
for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) { var c = new Vector2Int(x, y); if (free(c) && free(c + Vector2Int.right)) spots.Add(c); }
var a = spots.First(c => spots.Any(d => Mathf.Abs(d.x - c.x) >= 3 || Mathf.Abs(d.y - c.y) >= 3));
var far = spots.First(d => Mathf.Abs(d.x - a.x) >= 3 || Mathf.Abs(d.y - a.y) >= 3);
put(rn, a); put(mutou, far);
int rnBase = rn.DEF, mutouBase = mutou.DEF;
sb.Append("2 離れている: RN 防 " + rnBase + "（同志 " + AbilitySystem.ComradeDEF(rn) + "、ひとり " + lonely(rn) + "）・無糖 防 " + mutouBase + "\n");
put(mutou, a + Vector2Int.right);
sb.Append("2b 隣: RN 防 " + rn.DEF + "（同志 " + AbilitySystem.ComradeDEF(rn) + "）・無糖 防 " + mutou.DEF + "（同志 " + AbilitySystem.ComradeDEF(mutou) + "）\n");

// 3) 能力無効: 封じられた RN は上がらない（無糖は RN が隣にいるので上がったまま）
rn.isSealed = true;
sb.Append("3 RN 封印: RN 防 " + rn.DEF + "・無糖 防 " + mutou.DEF + "\n");
rn.isSealed = false;

// 4) 敵の駒は仲間に数えない
var enemy = bm.GetTeamPieces(Team.Enemy).First(e => e.isAlive && e.data.pieceType != PieceType.C3);
put(mutou, far);
var spot = a + Vector2Int.up; if (!free(spot)) spot = a + Vector2Int.down;
var enemyFrom = enemy.boardPosition; put(enemy, spot);
sb.Append("4 隣が敵だけ: RN 防 " + rn.DEF + "（同志 " + AbilitySystem.ComradeDEF(rn) + "）\n");
put(enemy, enemyFrom);

// 5) 仲間が倒れると会長の攻撃が下がる
mutou.isAlive = false; rn.isAlive = false;
GameSim.RunSync(ab.ExecuteTurnStartAbilities(Team.Player));
sb.Append("5 仲間が0人: 連合 +" + negu.allianceATK + "（攻 " + negu.ATK + "）\n");
return sb.ToString();
