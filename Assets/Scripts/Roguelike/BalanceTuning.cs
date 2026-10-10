/// <summary>
/// 局の流れまわりのバランスの数値。駒ごとの数値は PieceData のアセットにある。
/// 自動プレイの実験では、ここを一時的に書き換えて調整前後を比べる（書き換えたら元に戻す）
/// </summary>
public static class BalanceTuning
{
    /// <summary>敵の攻撃の上乗せ = 局 ÷ この値（7なら第七局から+1、第十四局から+2）</summary>
    public static int EnemyAtkDivisor = 7;
    /// <summary>敵の体力の上乗せ = 局 ÷ この値</summary>
    public static int EnemyHpDivisor = 3;
    /// <summary>敵C3の体力の上乗せ = (局−1) ÷ この値</summary>
    public static int EnemyC3HpDivisor = 2;
    /// <summary>敵C3の体力にさらに足す値（自軍のC3とは別に調整するため）</summary>
    public static int EnemyC3ExtraHP = 0;

    /// <summary>自軍の体力の上乗せ = (局−1) ÷ この値（C3も含む）</summary>
    public static int PlayerHpDivisor = 4;
    /// <summary>自軍の防御の上乗せ = (局−1) ÷ この値（C3は除く）</summary>
    public static int PlayerDefDivisor = 5;

    /// <summary>仲間を2枚選べる局（盤が広がる局）</summary>
    public static int[] TwoPickStages = { 3, 5 };

    /// <summary>仲間にした素の将棋駒の体力の上乗せ</summary>
    public static int RecruitBonusHP = 1;
    /// <summary>仲間にした飛車・角の攻撃の上乗せ</summary>
    public static int RecruitBonusATK = 1;
}
