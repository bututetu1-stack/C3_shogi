/// <summary>
/// 勝ち負けに関わる乱数（能力の向きや的・AI のほぼ同点の手の選び方・仲間の候補・布陣の型・部の時間など）。
/// 演出の乱数（UnityEngine.Random。揺れ・火の粉など）とは分けて、種が同じなら同じ並びになるようにする。
/// 対人戦では2人のゲームに同じ種を入れて、同じ手から同じ盤を作る。自動プレイの種もここに入れる。
/// </summary>
public static class GameRng
{
    private static System.Random rng = new System.Random(System.Environment.TickCount);

    /// <summary>種を決める（同じ種なら、このあとの乱数は同じ並びになる）</summary>
    public static void InitState(int seed)
    {
        rng = new System.Random(seed);
    }

    /// <summary>min 以上 max 未満の整数（max が min 以下なら min）</summary>
    public static int Range(int min, int max)
    {
        return max <= min ? min : rng.Next(min, max);
    }

    /// <summary>min 以上 max 未満の小数</summary>
    public static float Range(float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    /// <summary>0 以上 1 未満の小数</summary>
    public static float value
    {
        get { return (float)rng.NextDouble(); }
    }
}
