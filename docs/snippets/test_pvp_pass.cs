// 対人戦で、手番の側がパスする（艦隊の攻撃や帰投を進めるため）
var gm = GameManager.Instance;
if (gm.IsTurnProcessing) return "処理中";
gm.EndTurn();
return "パス（" + gm.currentTurn + "）";
