/// <summary>
/// 局の流れと駒の能力まわりのバランスの数値。駒ごとの攻撃・防御・体力・動きは PieceData のアセットにある。
/// 自動プレイの実験では、ここを一時的に書き換えて調整前後を比べる（書き換えたら元に戻す）
/// </summary>
public static class BalanceTuning
{
    /// <summary>敵の攻撃の上乗せ = (局 − EnemyAtkDelay) ÷ この値（4・1なら第五局から+1、第九局+2、第十三局+3）</summary>
    public static int EnemyAtkDivisor = 4;
    /// <summary>敵の攻撃の上乗せを何局遅らせるか（体力の上乗せと同じ局で一度に上がらないように）</summary>
    public static int EnemyAtkDelay = 1;
    /// <summary>敵の体力の上乗せ = 局 ÷ この値（2なら第二局から+1、第十五局で+7）</summary>
    public static int EnemyHpDivisor = 2;
    /// <summary>敵C3の体力の上乗せ = (局−1) ÷ この値</summary>
    public static int EnemyC3HpDivisor = 2;
    /// <summary>敵C3の体力にさらに足す値（自軍のC3とは別に調整するため）</summary>
    public static int EnemyC3ExtraHP = 0;

    /// <summary>自軍の体力の上乗せ = (局−1) ÷ この値（C3も含む）</summary>
    public static int PlayerHpDivisor = 4;
    /// <summary>自軍の防御の上乗せ = (局−1) ÷ この値（C3は除く）</summary>
    public static int PlayerDefDivisor = 5;

    /// <summary>仲間を2枚選べる局（盤が9×9になり、駒の数が一気に増える第五局）</summary>
    public static int[] TwoPickStages = { 5 };

    /// <summary>仲間の候補に素の将棋駒（香・桂・銀・金・角・飛）も出すか（いまは部員と強化だけ）</summary>
    public static bool DraftStandardPieces = false;

    /// <summary>仲間にした素の将棋駒の体力の上乗せ（DraftStandardPieces のときだけ使う）</summary>
    public static int RecruitBonusHP = 1;
    /// <summary>仲間にした飛車・角の攻撃の上乗せ</summary>
    public static int RecruitBonusATK = 1;

    // ---- 駒の能力（駒の攻撃・防御・体力・動きは PieceData のアセット） ----

    /// <summary>ヲツが手番の開始時に作る中華の数（成る前／成った後）</summary>
    public static int WotsuChukaCount = 2;
    public static int WotsuPromotedChukaCount = 3;
    /// <summary>中華を味方のそばに置く（false なら盤のどこか）</summary>
    public static bool ChukaNearAllies = true;
    /// <summary>中華が周りの味方を回復する量</summary>
    public static int ChukaHeal = 3;
    /// <summary>中華を食べた味方の「満腹」（攻撃+1）を1体が受けられる回数（その局のあいだ）</summary>
    public static int ChukaFullMax = 2;
    /// <summary>小錦が攻撃されたときの「同情」（周りの味方の攻撃+1）を1体が受けられる回数（その局のあいだ）</summary>
    public static int KonishikiSympathyMax = 2;

    /// <summary>ユウの助走: 敵まで走ったマスのうち、2マス目から1マスごとの攻撃の上乗せ（3マス先なら+2）</summary>
    public static int YuuRunUpPerSquare = 1;

    /// <summary>きぷが冷笑する敵の数（へるは周りの敵すべて）</summary>
    public static int KipuSneerTargets = 2;

    /// <summary>艦娘の空爆が巻き込む敵の数</summary>
    public static int AirRaidSplashTargets = 2;
    /// <summary>艦娘の砲撃の巻き込みダメージ</summary>
    public static int BombardmentSplashDamage = 1;
}
