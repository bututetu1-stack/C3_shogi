// test_fleet3.cs のあと数秒進めてから流す: 基地航空隊のあとの敵の体力、陣形、艦娘の位置、制空権を返す
var bm = BoardManager.Instance;
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
var sb = new System.Text.StringBuilder();
sb.Append("基地航空隊のあと: 敵の体力の合計 " + bm.GetTeamPieces(Team.Enemy).Where(p => p.data.pieceType != PieceType.C3 && !PieceTypes.IsShinkai(p.data.pieceType)).Sum(p => p.currentHP) + "\n");
var mono = bm.GetTeamPieces(Team.Player).FirstOrDefault(p => p.data.pieceType == PieceType.Monotetsu);
if (mono == null) return sb.Append("提督がいない").ToString();
var fleets = (System.Collections.IDictionary)typeof(AbilitySystem).GetField("fleets", bf).GetValue(AbilitySystem.Instance);
foreach (System.Collections.DictionaryEntry kv in fleets)
{
    var formation = kv.Value.GetType().GetField("formation").GetValue(kv.Value);
    sb.Append("艦隊" + kv.Key + " 陣形 " + formation + "\n");
}
sb.Append("提督 " + mono.boardPosition + " 警戒陣の軽減 " + AbilitySystem.FormationGuard(mono) + "\n");
foreach (var p in bm.GetTeamPieces(Team.Player).Where(p => PieceTypes.IsKanmusu(p.data.pieceType)))
    sb.Append("  " + p.data.displayName + " " + p.boardPosition + " 防御" + p.DEF + "\n");
sb.Append("制空権 " + typeof(AbilitySystem).GetMethod("HasAirSuperiority", bf).Invoke(null, null) + "\n");
return sb.ToString();
