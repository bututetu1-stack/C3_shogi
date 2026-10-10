using UnityEngine;

/// <summary>BGM と効果音の音量（0〜1、PlayerPrefs に保存）。設定画面から変える</summary>
public static class SoundSettings
{
    private const string BgmKey = "c3_bgm_volume";
    private const string SeKey = "c3_se_volume";
    // 以前の「音量」1本のときの保存先（あれば最初の値として引き継ぐ）
    private const string OldMasterKey = "c3_volume";

    private static bool loaded;
    private static float bgm = 0.8f;
    private static float se = 0.8f;

    /// <summary>音量が変わったとき（BattleEffects が鳴っている音に反映する）</summary>
    public static event System.Action Changed;

    public static float Bgm { get { Load(); return bgm; } set { Load(); bgm = Mathf.Clamp01(value); PlayerPrefs.SetFloat(BgmKey, bgm); if (Changed != null) Changed(); } }
    public static float Se { get { Load(); return se; } set { Load(); se = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SeKey, se); if (Changed != null) Changed(); } }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        float old = PlayerPrefs.GetFloat(OldMasterKey, 0.8f);
        bgm = PlayerPrefs.GetFloat(BgmKey, old);
        se = PlayerPrefs.GetFloat(SeKey, old);
    }
}
