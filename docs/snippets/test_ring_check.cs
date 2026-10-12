// test_ring.cs のあとに流す: 出撃した艦娘が改二・指輪か、GiveRing で艦種が覚えられるか、指輪で能力のダメージが増えるか
var gm = GameManager.Instance; var bm = BoardManager.Instance; var ab = AbilitySystem.Instance;
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
var sb = new System.Text.StringBuilder();
var ships = bm.GetTeamPieces(Team.Player).Where(p => PieceTypes.IsKanmusu(p.data.pieceType)).ToList();
foreach (var s in ships)
    sb.Append(s.data.pieceType + " 改 " + s.isPromoted + " 改二 " + s.kai2 + " 指輪 " + s.ringed + " 体力 " + s.currentHP + "/" + s.MaxHP + "（成った体力 " + s.data.promotedHP + "）\n");
if (ships.Count == 0) return sb.Append("艦娘がいない").ToString();
var shipDamage = typeof(AbilitySystem).GetMethod("ShipDamage", bf);
var s0 = ships[0];
int withRing = (int)shipDamage.Invoke(null, new object[] { s0, 2, null });
s0.ringed = false;
int without = (int)shipDamage.Invoke(null, new object[] { s0, 2, null });
s0.ringed = true;
sb.Append("能力のダメージ（基本2）: 指輪あり " + withRing + " / なし " + without + "\n");
// MVP の指輪: 覚えていない艦種の改二に GiveRing
gm.RingedClasses.Clear();
s0.ringed = false;
typeof(AbilitySystem).GetMethod("GiveRing", bf).Invoke(ab, new object[] { s0 });
sb.Append("GiveRing（改二）→ 覚えた艦種 " + string.Join(",", gm.RingedClasses) + " 指輪 " + s0.ringed + "\n");
var s1 = ships.Count > 1 ? ships[1] : null;
if (s1 != null) { bool k = s1.kai2; s1.kai2 = false; typeof(AbilitySystem).GetMethod("GiveRing", bf).Invoke(ab, new object[] { s1 }); s1.kai2 = k;
    sb.Append("GiveRing（改二でない）→ 覚えた艦種 " + string.Join(",", gm.RingedClasses) + "（増えないのが正しい）\n"); }
return sb.ToString();
