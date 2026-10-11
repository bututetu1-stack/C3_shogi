// 第9版の敵駒8種のアセットを作る（あれば上書き）。Assets/Resources/Pieces に置くので、BoardManager が自動で読む
MoveDirection M(int dx, int dy, int dist, bool jump = false) { return new MoveDirection(dx, dy, dist, jump); }
MoveDirection[] All(int dist) { return new[] { M(0,1,dist), M(0,-1,dist), M(1,0,dist), M(-1,0,dist), M(1,1,dist), M(1,-1,dist), M(-1,1,dist), M(-1,-1,dist) }; }
var made = new System.Collections.Generic.List<string>();
void Make(string file, PieceType type, string name, string flavor, string desc, int atk, int def, int hp, MoveDirection[] moves,
          bool immovable = false, bool taunt = false)
{
    string path = "Assets/Resources/Pieces/" + file + ".asset";
    var d = AssetDatabase.LoadAssetAtPath<PieceData>(path);
    bool isNew = d == null;
    if (isNew) d = ScriptableObject.CreateInstance<PieceData>();
    d.pieceType = type;
    d.displayName = name; d.pieceName = name;
    d.flavor = flavor; d.description = desc;
    d.baseATK = atk; d.baseDEF = def; d.baseHP = hp;
    d.rarity = Rarity.Normal;
    d.moveDirections = moves;
    d.canPromote = false;
    d.promotedMoveDirections = new MoveDirection[0];
    d.awakenedMoveDirections = new MoveDirection[0];
    d.veteranMoveDirections = new MoveDirection[0];
    d.isImmovable = immovable;
    d.isTauntPiece = taunt;
    d.excludeFromDraft = true;
    d.isManualControllable = true;
    if (isNew) AssetDatabase.CreateAsset(d, path); else EditorUtility.SetDirty(d);
    made.Add(file + (isNew ? "(new)" : "(updated)"));
}

Make("Kyogan", PieceType.Kyogan, "巨岩", "戦場に転がる大きな岩。",
    "動けない地形。敵も味方も通れないが、攻撃して壊せる（勝敗・撃破数・練度には数えない）。",
    0, 1, 3, new MoveDirection[0], true);
Make("Kugutsu", PieceType.Kugutsu, "傀儡", "糸で操られる木の人形。",
    "動けない。挑発: この駒を攻撃できる自軍の駒は、ほかの駒を攻撃できない（勝敗・撃破数・練度には数えない）。",
    0, 0, 5, new MoveDirection[0], true, true);
Make("Yumihei", PieceType.Yumihei, "弓兵", "遠くから狙う弓の名手。",
    "敵の手番の終わりに、前の直線上でいちばん近い自軍の駒を射る（1ダメージ、防御無視。攻撃の上乗せぶん増える）。C3 や敵の駒に当たると矢はそこで止まる。",
    1, 0, 2, new[] { M(0,1,1), M(1,0,1), M(-1,0,1) });
Make("Ootate", PieceType.Ootate, "大盾", "大きな盾を構えた兵。",
    "正面（自軍の側）から受ける通常の攻撃のダメージを2減らす。横や後ろからは普通に通る。",
    2, 1, 4, new[] { M(0,1,1), M(1,0,1), M(-1,0,1) });
Make("Chototsu", PieceType.Chototsu, "猪突", "猪突猛進の荒武者。",
    "前にどこまでも突き進む。助走: 走って攻撃すると、2マス目から1マスごとに攻撃+1。",
    2, 0, 3, new[] { M(0,1,8), M(1,0,1), M(-1,0,1) });
Make("Youko", PieceType.Youko, "妖狐", "人を化かす狐の妖。",
    "倒されると、隣に狐火を2つ残す。",
    2, 1, 2, All(1));
Make("Kitsunebi", PieceType.Kitsunebi, "狐火", "妖狐が残した青白い火。",
    "全方向に1マス動く。",
    1, 0, 1, All(1));
Make("Kekkai", PieceType.Kekkai, "結界", "結界を張る術者。",
    "能力無効: 周り2マスにいる自軍の駒は能力が止まる（★や覚醒の数値はそのまま）。結界を倒すか、範囲から出ると元に戻る。",
    0, 2, 4, new[] { M(0,1,1), M(0,-1,1), M(1,0,1), M(-1,0,1) });

AssetDatabase.SaveAssets();
return string.Join(", ", made);
