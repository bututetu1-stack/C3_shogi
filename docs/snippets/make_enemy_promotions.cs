// 2026-10-12: 新しい敵（亡霊・僧兵）のアセットを作り、敵専用の駒に成った姿を付け、龍神をレジェンドにする（何度流しても同じ結果）
MoveDirection M(int dx, int dy, int dist, bool jump = false) { return new MoveDirection(dx, dy, dist, jump); }
MoveDirection[] All(int dist) { return new[] { M(0,1,dist), M(0,-1,dist), M(1,0,dist), M(-1,0,dist), M(1,1,dist), M(1,-1,dist), M(-1,1,dist), M(-1,-1,dist) }; }
MoveDirection[] GoldMoves() { return new[] { M(0,1,1), M(1,1,1), M(-1,1,1), M(1,0,1), M(-1,0,1), M(0,-1,1) }; }
var log = new System.Collections.Generic.List<string>();
var byType = new System.Collections.Generic.Dictionary<PieceType, PieceData>();
foreach (var guid in AssetDatabase.FindAssets("t:PieceData"))
{
    var d = AssetDatabase.LoadAssetAtPath<PieceData>(AssetDatabase.GUIDToAssetPath(guid));
    if (d != null && !byType.ContainsKey(d.pieceType)) byType[d.pieceType] = d;
}

PieceData MakeNew(string file, PieceType type, string name, string flavor, string desc, int atk, int def, int hp, MoveDirection[] moves)
{
    string path = "Assets/Resources/Pieces/" + file + ".asset";
    var d = AssetDatabase.LoadAssetAtPath<PieceData>(path);
    bool isNew = d == null;
    if (isNew) d = ScriptableObject.CreateInstance<PieceData>();
    d.pieceType = type;
    d.displayName = name; d.pieceName = name;
    d.flavor = flavor; d.description = desc;
    d.baseATK = atk; d.baseDEF = def; d.baseHP = hp;
    d.rarity = Rarity.Rare;
    d.moveDirections = moves;
    d.awakenedMoveDirections = new MoveDirection[0];
    d.veteranMoveDirections = new MoveDirection[0];
    d.excludeFromDraft = true;
    d.isManualControllable = true;
    if (isNew) AssetDatabase.CreateAsset(d, path); else EditorUtility.SetDirty(d);
    byType[type] = d;
    log.Add(file + (isNew ? "（新規）" : "（更新）"));
    return d;
}

void Promo(PieceType type, string face, string name, string desc, int dAtk, int dDef, int dHp, MoveDirection[] moves = null)
{
    var d = byType[type];
    d.canPromote = true;
    d.promotedDisplayName = face;
    d.promotedName = name;
    d.promotedDescription = desc;
    d.promotedATK = d.baseATK + dAtk;
    d.promotedDEF = d.baseDEF + dDef;
    d.promotedHP = d.baseHP + dHp;
    d.promotedMoveDirections = moves ?? (MoveDirection[])d.moveDirections.Clone();
    EditorUtility.SetDirty(d);
    log.Add(d.pieceName + " → " + name + "（" + face + "）" + d.promotedATK + "/" + d.promotedDEF + "/" + d.promotedHP);
}

// ---- 新しい敵 ----
MakeNew("Bourei", PieceType.Bourei, "亡霊", "夜な夜な盤をさまよう霊。刃はその身をすり抜ける。",
    "すり抜け: 通常の攻撃ではダメージを受けず、攻撃してきた向きへ1マス押し返される（後ろが空いていれば）。能力のダメージは受ける。",
    2, 0, 2, All(1));
var souhei = MakeNew("Souhei", PieceType.Souhei, "僧兵", "法力で身を守る修行僧。術は通じないが、刀は通る。",
    "破魔: 相手の能力のダメージや効果（弱体・冷笑・押し出し・裏返し・挑発・組み止めなど）を受けない。通常の攻撃は普通に受ける。",
    2, 1, 3, GoldMoves());
souhei.immuneToFlip = true;

// ---- 成った姿（相手の陣地＝こちらの手前3段に入ると成る） ----
Promo(PieceType.Kishou, "鬼神", "鬼神", "鬼将が成った姿。全方向に1マス動ける。攻撃と体力がさらに高い。", 1, 0, 1);
var kagenin = byType[PieceType.Kagenin];
var jonin = new System.Collections.Generic.List<MoveDirection>(kagenin.moveDirections);
jonin.AddRange(new[] { M(0,1,1), M(0,-1,1), M(1,0,1), M(-1,0,1) });
Promo(PieceType.Kagenin, "上忍", "上忍", "影忍が成った姿。斜めにどこまでも、縦横に1マス動ける。", 0, 0, 1, jonin.ToArray());
Promo(PieceType.Teppeki, "金城", "金城", "鉄壁が成った姿。全方向に1マス動けるようになり、防御がさらに高い。", 0, 1, 0, All(1));
Promo(PieceType.Tengu, "天狗", "大天狗", "天狗が成った姿。障害物を飛び越える跳躍はそのまま。攻撃がさらに高い。", 1, 0, 0);
Promo(PieceType.Enmashi, "閻王", "閻魔大王", "自分の手番の終わりに、周り2マスの味方の体力を2回復する。", 0, 0, 0);
Promo(PieceType.Gundaishou, "将軍", "大将軍", "自分の手番の終わりに、周り2マスの味方の攻撃を+1する（1体3回まで）。", 0, 0, 0);
var fujin = byType[PieceType.Fujin];
var ranshin = new System.Collections.Generic.List<MoveDirection>();
foreach (var m in fujin.moveDirections)
{
    bool orth = m.direction.x == 0 || m.direction.y == 0;
    ranshin.Add(orth ? M(m.direction.x, m.direction.y, 9, m.canJump) : m);
}
Promo(PieceType.Fujin, "嵐神", "嵐神", "風神が成った姿。縦横斜めにどこまでも動ける。攻撃がさらに高い。", 1, 0, 0, ranshin.ToArray());
Promo(PieceType.Dokuro, "餓者", "餓者髑髏", "倒されると爆発し、隣のすべての駒（相手も味方も）に3ダメージ（防御で減る。攻撃の上乗せぶん増える）。", 0, 0, 0);
Promo(PieceType.Chototsu, "猛猪", "猛猪", "前にどこまでも突き進む。助走: 走って攻撃すると、2マス目から1マスごとに攻撃+2。", 0, 0, 0);
Promo(PieceType.Youko, "九尾", "九尾", "倒されると、隣に狐火を3つ残す。攻撃がさらに高い。", 1, 0, 0);
Promo(PieceType.Kitsunebi, "鬼火", "鬼火", "狐火が成った姿。全方向に1マス動く。攻撃がさらに高い。", 1, 0, 0);
Promo(PieceType.Yumihei, "弩兵", "弩兵", "自分の手番の終わりに、前の直線上でいちばん近い相手の駒を射る（3ダメージ、防御で減る。攻撃の上乗せぶん増える）。味方の駒や C3 に当たると、矢はそこで止まる。", 0, 0, 0);
Promo(PieceType.Ootate, "鉄盾", "鉄大盾", "正面（相手の側）から受ける通常の攻撃のダメージを3減らし、横や後ろからも1減らす。", 0, 0, 0);
Promo(PieceType.Yomigaeru, "黄泉", "黄泉大王", "自分の手番の初めに、自陣に歩兵を1枚呼び出す（最大3回）。", 0, 0, 0);
Promo(PieceType.Bourei, "怨霊", "怨霊", "亡霊が成った姿。すり抜けはそのまま。攻撃と防御がさらに高い。", 1, 1, 0);
Promo(PieceType.Souhei, "法師", "法師", "僧兵が成った姿。破魔はそのまま。攻撃と防御がさらに高い。", 1, 1, 0);

// ---- 覚醒するのはレジェンドの駒だけ（雷帝・魔王に、龍神もそろえる） ----
byType[PieceType.Ryuujin].rarity = Rarity.Legend;
EditorUtility.SetDirty(byType[PieceType.Ryuujin]);
log.Add("龍神 → レジェンド");

AssetDatabase.SaveAssets();
return string.Join("\n", log);
