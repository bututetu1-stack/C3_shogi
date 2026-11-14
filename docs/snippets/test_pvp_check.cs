// test_pvp.cs のあと: 艦娘の陣営と位置、提督の位置、手番、先手の駒の体力
var gm = GameManager.Instance; var bm = BoardManager.Instance;
var ships = bm.GetTeamPieces(Team.Enemy).Where(p => PieceTypes.IsKanmusu(p.data.pieceType)).Select(p => p.data.pieceType + "@" + p.boardPosition + " 体力" + p.currentHP);
var wrong = bm.GetTeamPieces(Team.Player).Where(p => PieceTypes.IsKanmusu(p.data.pieceType) || PieceTypes.IsShinkai(p.data.pieceType)).Count()
    + bm.GetTeamPieces(Team.Enemy).Where(p => PieceTypes.IsShinkai(p.data.pieceType)).Count();
var teitoku = bm.GetTeamPieces(Team.Enemy).FirstOrDefault(p => p.data.pieceType == PieceType.Monotetsu);
return "手番 " + gm.currentTurn + " " + gm.MoveCount + "手目 | 後手の艦娘 " + string.Join(", ", ships)
    + " | 深海や先手の艦娘（0が正しい） " + wrong
    + " | 物鉄 " + (teitoku == null ? "なし" : teitoku.boardPosition + (teitoku.isPromoted ? " 提督" : " 物鉄・改"))
    + " | 先手 " + string.Join(",", bm.GetTeamPieces(Team.Player).Where(p => !AbilitySystem.IsObstacle(p.data.pieceType)).Select(p => p.data.pieceType + ":" + p.currentHP));
