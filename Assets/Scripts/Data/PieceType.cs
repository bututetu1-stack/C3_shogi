public enum PieceType
{
    Pawn,    // 歩
    Lance,   // 香
    Knight,  // 桂
    Silver,  // 銀
    Gold,    // 金
    Bishop,  // 角
    Rook,    // 飛
    C3,      // サークル(動けない王の代替)
    Monin,   // 門人
    Wotsu,   // ヲツ
    Chuka,      // 中華
    Rihaku,     // 李白
    Monotetsu,  // 物鉄
    Shinkai,    // 深海
    Kanmusu,    // 艦娘
    SN,         // SN
    Boku,       // 僕
    Nako,       // なこ
    Dopa,       // ドパ
    Konishiki,  // 小錦
    Kishou,     // 鬼将 (敵専用)
    Kagenin,    // 影忍 (敵専用)
    Teppeki,    // 鉄壁 (敵専用)
    Tengu,      // 天狗 (敵専用)
    Raitei,     // 雷帝 (敵専用)
    Enmashi,    // 閻魔 (敵専用)
    Gundaishou, // 軍将 (敵専用)
    Yomigaeru,  // 黄泉 (敵専用)
    Fujin,      // 風神 (敵専用)
    Dokuro,     // 髑髏 (敵専用)
    Ryuujin,    // 龍神 (敵専用)
    Maou        // 魔王 (敵専用)
}

public enum Team
{
    Player,
    Enemy
}

public enum GamePhase
{
    PieceSelection,
    Battle,
    GameOver
}

public enum Rarity
{
    Normal,     // 0 - ノーマル (茶)
    Rare,       // 1 - レア (銀)
    SuperRare,  // 2 - 激レア (金)
    Legend,     // 3 - レジェンド
    Bronze      // 4 - 銅
}
