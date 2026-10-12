// 2026-10-12 の敵の仕組みを盤の上で確かめる（プレイ中に流す）: 亡霊のすり抜け・僧兵の破魔・敵の成り・敵の AI のただ取られ除け
var gm = GameManager.Instance; var bm = BoardManager.Instance; var ab = AbilitySystem.Instance;
var sel = UnityEngine.Object.FindFirstObjectByType<PieceSelectionUI>(); if (sel != null) sel.Hide();
var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>(); if (title != null) UnityEngine.Object.Destroy(title.gameObject);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
GameSim.Headless = true;
gm.SimBeginRun();
StageManager.Instance.currentStage = 6;
typeof(GameManager).GetMethod("SetupStageBoard", bf).Invoke(gm, null);
foreach (var t in new[] { PieceType.Kei, PieceType.Konishiki, PieceType.Kipu })
    gm.SimApplyOption(DraftOption.Piece(bm.GetPieceDataByType(t)));
typeof(GameManager).GetMethod("StartBattle", bf).Invoke(gm, null);

var sb = new System.Text.StringBuilder();
int size = bm.CurrentBoardSize;
System.Func<PieceType, Team, PieceInstance> P = (t, team) => bm.GetTeamPieces(team).FirstOrDefault(p => p.data.pieceType == t && p.isAlive);
// 空いたマスに駒を置く（盤の真ん中あたり）
System.Func<PieceType, Team, Vector2Int, PieceInstance> Spawn = (t, team, c) =>
{
    var old = bm.GetPieceAt(c); if (old != null) bm.RemovePieceFromBoard(c);
    var inst = new PieceInstance(bm.GetPieceDataByType(t), team, c);
    bm.PlacePieceOnBoard(inst, c);
    return inst;
};
System.Action<PieceInstance, Vector2Int> put = (p, c) => { bm.RemovePieceFromBoard(p.boardPosition); bm.PlacePieceOnBoard(p, c); };
// 真ん中の列の4〜5段目を空ける
for (int y = 3; y <= 5; y++) for (int x = 0; x < size; x++) { var c = new Vector2Int(x, y); if (!bm.IsEmpty(c)) bm.RemovePieceFromBoard(c); }

// 1) 亡霊: 通常の攻撃はすり抜けて押し返される・能力のダメージは受ける
var ghost = Spawn(PieceType.Bourei, Team.Enemy, new Vector2Int(4, 4));
var kipu = P(PieceType.Kipu, Team.Player); put(kipu, new Vector2Int(4, 3));
int hp0 = ghost.currentHP;
sb.Append("1 亡霊 予想ダメージ " + CombatResolver.CalcDamage(kipu, ghost) + "\n");
bool killed = CombatResolver.Attack(kipu, ghost);
sb.Append("1b 攻撃: 倒れた " + killed + " 体力 " + hp0 + "→" + ghost.currentHP + " 位置 (4,4)→" + ghost.boardPosition + "\n");
CombatResolver.AbilityHit(ghost, 1);
sb.Append("1c 能力 2ダメージ相当: 体力 " + ghost.currentHP + "（生きている " + ghost.isAlive + "）\n");

// 2) 僧兵: 能力のダメージ・弱体・挑発・組み止めを受けない、通常の攻撃は受ける
var monk = Spawn(PieceType.Souhei, Team.Enemy, new Vector2Int(2, 4));
int mhp = monk.currentHP;
CombatResolver.AbilityHit(monk, 3);
sb.Append("2 僧兵 能力: 体力 " + mhp + "→" + monk.currentHP + "\n");
var kei = P(PieceType.Kei, Team.Player); put(kei, new Vector2Int(2, 3));
var adj = typeof(AbilitySystem).GetMethod("AdjacentEnemies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { kei }) as System.Collections.Generic.List<PieceInstance>;
sb.Append("2b 鶏炭の隣の敵に僧兵が入るか " + adj.Contains(monk) + "\n");
var kon = P(PieceType.Konishiki, Team.Player); put(kon, new Vector2Int(1, 3));
var moves = MoveValidator.GetValidMoves(monk);
sb.Append("2c 小錦の隣でも動ける手 " + moves.Count(m => !m.isAttack) + "、攻撃の的 " + string.Join(",", moves.Where(m => m.isAttack).Select(m => bm.GetPieceAt(m.position).DisplayName)) + "\n");
int dmg = CombatResolver.CalcDamage(kei, monk);
CombatResolver.Attack(kei, monk);
sb.Append("2d 通常の攻撃: 予想 " + dmg + " 体力 " + monk.currentHP + "\n");

// 3) 敵の成り: 鬼将がこちらの陣地に入ると鬼神になる
var oni = Spawn(PieceType.Kishou, Team.Enemy, new Vector2Int(6, 3));
int a0 = oni.ATK;
var dest = new Vector2Int(6, 2);
if (!bm.IsEmpty(dest)) bm.RemovePieceFromBoard(dest);
var mv = MoveValidator.GetValidMoves(oni).First(m => m.position == dest);
GameSim.RunSync(CombatResolver.ExecuteMove(oni, mv));
sb.Append("3 鬼将が (6,2) へ: 成った " + oni.isPromoted + " 名前 " + oni.FullName + "（面 " + oni.DisplayName + "） 攻 " + a0 + "→" + oni.ATK + "\n");
var shield = Spawn(PieceType.Ootate, Team.Enemy, new Vector2Int(7, 5));
var front = Spawn(PieceType.Pawn, Team.Player, new Vector2Int(7, 4));
var side = Spawn(PieceType.Pawn, Team.Player, new Vector2Int(8, 5));
int g0 = CombatResolver.ShieldGuard(front, shield), s0 = CombatResolver.ShieldGuard(side, shield);
shield.Promote();
sb.Append("3b 大盾の軽減 正面/横 " + g0 + "/" + s0 + " → 鉄大盾 " + CombatResolver.ShieldGuard(front, shield) + "/" + CombatResolver.ShieldGuard(side, shield) + "\n");

GameSim.Headless = false;
return sb.ToString();
