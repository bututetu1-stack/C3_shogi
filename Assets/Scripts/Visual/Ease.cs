using UnityEngine;

/// <summary>アニメーション用のイージング関数（t: 0..1）</summary>
public static class Ease
{
    public static float OutCubic(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
    public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
    public static float InOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }
    public static float OutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
    /// <summary>0→1→0 の山なり</summary>
    public static float Arc(float t) { t = Mathf.Clamp01(t); return Mathf.Sin(t * Mathf.PI); }
}
