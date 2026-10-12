// AI の読みの確かめ（プレイ中に流す）。空の盤に駒を並べて、工夫なし（0）と全部入り（15）で1手を選ばせる。
//   問1: ただで取れる駒（体力1の金将）を飛車で取るか
//   問2: 自軍 C3（体力2）を狙う敵の飛車を、取るか間に入って防ぐか
//   問3: 敵側で、問1と同じ形（敵の飛車がただで取れる自軍の金将）を取るか
var gm = GameManager.Instance; var bm = BoardManager.Instance;
var sel = UnityEngine.Object.FindFirstObjectByType<PieceSelectionUI>(); if (sel != null) sel.Hide();
var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>(); if (title != null) UnityEngine.Object.Destroy(title.gameObject);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
gm.SimBeginRun();
StageManager.Instance.currentStage = 10;
typeof(GameManager).GetMethod("SetupStageBoard", bf).Invoke(gm, null);
var ai = SimpleAI.Instance;
var sb = new System.Text.StringBuilder();

PieceInstance Put(PieceType t, Team team, int x, int y)
{
    return bm.Spawn(bm.GetPieceDataByType(t), team, new Vector2Int(x, y));
}
void Reset()
{
    bm.ClearAll();
    Put(PieceType.C3, Team.Player, 4, 0);
    Put(PieceType.C3, Team.Enemy, 4, 8);
    // C3 の前を歩で固めて、C3 を狙う手で答えが変わらないようにする
    for (int x = 3; x <= 5; x++) { Put(PieceType.Pawn, Team.Player, x, 1); Put(PieceType.Pawn, Team.Enemy, x, 7); }
}
string Choose(Team team, int features)
{
    BalanceTuning.AiFeaturesPlayer = features;
    BalanceTuning.AiFeaturesEnemy = features;
    PieceInstance pc; MoveValidator.MoveResult mv;
    ai.dumpRoot = true;
    if (!ai.TryChooseMove(team, 150f, out pc, out mv)) return "パス";
    ai.dumpRoot = false;
    return pc.data.displayName + (mv.isAttack ? "が攻撃" : "が移動") + mv.position + "\n    [" + ai.LastRootDump + "]";
}

// 問1
Reset();
var rook = Put(PieceType.Rook, Team.Player, 0, 3);
var gold = Put(PieceType.Gold, Team.Enemy, 0, 6);
gold.currentHP = 1;
sb.Append("問1（正解: 飛車が攻撃(0, 6)。C3 の筋に回る手と点が近い）: 工夫0 " + Choose(Team.Player, 0) + " / 工夫15 " + Choose(Team.Player, 15) + "\n");

// 問2
Reset();
var pc3 = bm.FindC3(Team.Player); pc3.currentHP = 2;
var erook = Put(PieceType.Rook, Team.Enemy, 4, 5);
erook.bonusATK += 10; erook.currentHP = 1;
CombatResolver.ApplyDamage(bm.GetPieceAt(new Vector2Int(4, 1)), 999, true);   // C3 の前の歩をどけて、飛車の筋を通す
Put(PieceType.Rook, Team.Player, 0, 5);
Put(PieceType.Pawn, Team.Player, 8, 1);
sb.Append("問2（正解: 飛車が攻撃(4, 5) か (4, 2〜4) に入って防ぐ）: 工夫0 " + Choose(Team.Player, 0) + " / 工夫15 " + Choose(Team.Player, 15) + "\n");

// 問3（敵側）
Reset();
Put(PieceType.Rook, Team.Enemy, 8, 5).bonusATK += 5;
var pgold = Put(PieceType.Gold, Team.Player, 8, 2);
pgold.currentHP = 1;
sb.Append("問3（正解: 飛車が攻撃(8, 2)。攻撃を上げた飛車なら C3 を詰ませる手でもよい。点 99990 台は詰み）: 工夫0 " + Choose(Team.Enemy, 0) + " / 工夫15 " + Choose(Team.Enemy, 15) + "\n");

BalanceTuning.AiFeaturesPlayer = 0;
BalanceTuning.AiFeaturesEnemy = 0;
return sb.ToString();
