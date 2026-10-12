// 対人戦の対局中に、後手の物鉄を提督に着任させる（test_pvp.cs → 並べ方のあと）
var gm = GameManager.Instance; var bm = BoardManager.Instance;
var mono = bm.GetTeamPieces(Team.Enemy).First(p => p.data.pieceType == PieceType.Monotetsu);
gm.StartCoroutine(gm.PromoteRoutine(mono));
return "後手の物鉄 " + mono.boardPosition + " を着任させる";
