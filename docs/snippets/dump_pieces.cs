string Esc(string s) { return s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", ""); }
string Dirs(MoveDirection[] ds) { if (ds == null) return "[]"; return "[" + string.Join(",", ds.Select(d => "[" + d.direction.x + "," + d.direction.y + "," + d.maxDistance + "," + (d.canJump ? 1 : 0) + "]")) + "]"; }
var items = new System.Collections.Generic.List<string>();
foreach (var g in AssetDatabase.FindAssets("t:PieceData")) {
  string path = AssetDatabase.GUIDToAssetPath(g);
  var p = AssetDatabase.LoadAssetAtPath<PieceData>(path);
  items.Add("{\"type\":\"" + p.pieceType + "\",\"path\":\"" + path + "\",\"name\":\"" + Esc(p.displayName) + "\",\"full\":\"" + Esc(p.pieceName) + "\",\"rarity\":\"" + p.rarity + "\""
    + ",\"atk\":" + p.baseATK + ",\"def\":" + p.baseDEF + ",\"hp\":" + p.baseHP + ",\"canPromote\":" + (p.canPromote ? "true" : "false")
    + ",\"pname\":\"" + Esc(p.promotedDisplayName) + "\",\"pfull\":\"" + Esc(p.promotedName) + "\",\"patk\":" + p.promotedATK + ",\"pdef\":" + p.promotedDEF + ",\"php\":" + p.promotedHP
    + ",\"prarity\":\"" + (p.hasPromotedRarity ? p.promotedRarity.ToString() : "") + "\""
    + ",\"moves\":" + Dirs(p.moveDirections) + ",\"pmoves\":" + Dirs(p.promotedMoveDirections)
    + ",\"immovable\":" + (p.isImmovable ? "true" : "false") + ",\"immovablePromoted\":" + (p.isImmovableWhenPromoted ? "true" : "false")
    + ",\"auto\":" + (p.isAutoMove ? "true" : "false") + ",\"manual\":" + (p.isManualControllable ? "true" : "false") + ",\"noDraft\":" + (p.excludeFromDraft ? "true" : "false")
    + ",\"desc\":\"" + Esc(p.description) + "\",\"pdesc\":\"" + Esc(p.promotedDescription) + "\",\"vdesc\":\"" + Esc(p.veteranDescription) + "\",\"vmoves\":" + Dirs(p.veteranMoveDirections)
    + ",\"canAwaken\":" + (p.canAwaken ? "true" : "false") + ",\"aname\":\"" + Esc(p.awakenedName) + "\",\"adesc\":\"" + Esc(p.awakenedDescription) + "\",\"acap\":\"" + Esc(p.awakenCaption) + "\""
    + ",\"aatk\":" + p.awakenedATK + ",\"adef\":" + p.awakenedDEF + ",\"ahp\":" + p.awakenedHP + ",\"amoves\":" + Dirs(p.awakenedMoveDirections) + "}");
}
System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../BalanceReports/pieces_dump.json"), "[" + string.Join(",\n", items) + "]", new System.Text.UTF8Encoding(false));
return items.Count;
