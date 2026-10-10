public enum PieceType
{
    Pawn,    // 歩
    Lance,   // 香
    Knight,  // 桂
    Silver,  // 銀
    Gold,    // 金
    Bishop,  // 角
    Rook,    // 飛
    C3,      // 部（動けない王の代わり）
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
    Maou,       // 魔王 (敵専用)
    Kei,        // けい（成ると異端）
    Yuu,        // ユウ
    Kipu,       // きぷ（成るとへる）
    // 艦娘の艦種（提督の着任時に建造で決まる。成ると改、改のまま敵陣を出ると改二）
    KanmusuDD,  // 駆逐
    KanmusuCL,  // 軽巡
    KanmusuCA,  // 重巡
    KanmusuBB,  // 戦艦
    KanmusuCV,  // 空母
    KanmusuSS,  // 潜水
    // 深海のランク（ノーマルは Shinkai）
    ShinkaiElite,
    ShinkaiFlagship,
    ShinkaiHime // 姫級
}

/// <summary>駒の種類のまとまり</summary>
public static class PieceTypes
{
    /// <summary>艦娘（艦種なしの Kanmusu も含む）</summary>
    public static bool IsKanmusu(PieceType type)
    {
        return type == PieceType.Kanmusu || (type >= PieceType.KanmusuDD && type <= PieceType.KanmusuSS);
    }

    /// <summary>深海（ノーマル・elite・flagship・姫級）</summary>
    public static bool IsShinkai(PieceType type)
    {
        return type == PieceType.Shinkai || (type >= PieceType.ShinkaiElite && type <= PieceType.ShinkaiHime);
    }
}

/// <summary>駒の数値の種類（攻撃・防御・体力）</summary>
public enum StatKind
{
    ATK,
    DEF,
    HP
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
    GameOver,
    Title,       // タイトル画面
    StageClear   // ステージクリアの演出中
}

public enum Rarity
{
    Normal,     // 0 - ノーマル (茶)
    Rare,       // 1 - レア (銀)
    SuperRare,  // 2 - 激レア (金)
    Legend,     // 3 - レジェンド
    Bronze      // 4 - 銅
}
