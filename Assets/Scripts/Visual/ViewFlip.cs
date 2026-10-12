using UnityEngine;

/// <summary>
/// 盤を180度回して見る（オンラインの対人戦で、後手の人の画面）。自分の駒が手前（画面の下）に来る。
/// カメラを回すので、盤の上の文字（駒の数字・飛び出す文字・座標・吹き出し・予想ダメージ）は Upright で正立させる。
/// </summary>
public static class ViewFlip
{
    public static bool Flipped { get; private set; }

    public static void Set(bool flipped)
    {
        Flipped = flipped;
        if (CameraFitter.Instance != null) CameraFitter.Instance.Refit();
    }

    /// <summary>画面の上の向き（盤の座標で）</summary>
    public static Vector3 Up { get { return Flipped ? Vector3.down : Vector3.up; } }

    /// <summary>画面の右の向き（盤の座標で）</summary>
    public static Vector3 Right { get { return Flipped ? Vector3.left : Vector3.right; } }

    /// <summary>盤を回して見ているときは、文字を逆さにして画面で正立させる</summary>
    public static void Upright(Transform t)
    {
        if (t != null && Flipped) t.rotation = Quaternion.Euler(0f, 0f, 180f);
    }
}
