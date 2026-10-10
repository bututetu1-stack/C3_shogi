using UnityEngine;

/// <summary>
/// 覚醒（成りのさらに上の姿）。練度★3の部員が、その局で活躍（倒す・能力が決まる）を
/// BalanceTuning.AwakenActivities 回重ねると、対局の途中でその場で覚醒する。
/// 数値の上乗せと動きの変化はここ、演出は GameManager.Awaken。
/// </summary>
public static class Awakening
{
    /// <summary>覚醒の数値を駒に付ける（盤や演出に触れない）。覚醒したら true</summary>
    public static bool Apply(PieceInstance p)
    {
        if (p == null || !p.isAlive || p.awakened || p.data == null || !p.data.canAwaken) return false;
        p.awakened = true;
        p.bonusATK += p.data.awakenedATK;
        p.bonusDEF += p.data.awakenedDEF;
        p.AddMaxHP(p.data.awakenedHP);
        return true;
    }

    /// <summary>押し出しで、target が押される先（押す駒から見て向こう側へ1マス）</summary>
    public static Vector2Int PushDestination(Vector2Int pusher, Vector2Int target)
    {
        Vector2Int step = new Vector2Int(System.Math.Sign(target.x - pusher.x), System.Math.Sign(target.y - pusher.y));
        return target + step;
    }
}
