// 対人戦の確かめ（プレイ中に流す）: 2人の編成を入れて対局を始め、後手の物鉄を提督に着任させる。
// このあと ui_press.cs で「準備ができた」「この並びで決める」を2回ずつ押して対局を始め、test_pvp_teitoku.cs で後手の物鉄を着任させる。
// 数秒進めてから test_pvp_check.cs で、艦娘が後手側に出たか・手番を回すと先手の駒を撃つか・10手で帰投するかを見る
var gm = GameManager.Instance; var bm = BoardManager.Instance;
var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>(); if (title != null) UnityEngine.Object.Destroy(title.gameObject);
PvpMatch.Begin(PvpMatch.Rule.Cost);
foreach (var t in new[] { PieceType.Konishiki, PieceType.Yuu, PieceType.Gold, PieceType.Pawn, PieceType.Pawn, PieceType.Yumihei })
    PvpMatch.First.pieces.Add(bm.GetPieceDataByType(t));
foreach (var t in new[] { PieceType.Monotetsu, PieceType.Niko, PieceType.Mitsuharu, PieceType.Silver })
    PvpMatch.Second.pieces.Add(bm.GetPieceDataByType(t));
PvpMatch.Second.upgrades.Add(UpgradeKind.AllATK);
gm.StartPvpBattle();
return "対局の準備（並べ方へ）";
