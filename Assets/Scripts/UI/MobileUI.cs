using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// スマホ（タッチの端末）向けの表示。横向き専用（2026-10-12 ユーザー決定。iPhone の Safari は全画面にできず、unityroom の画面は 16:9 のままなので）。
/// ・画面の文字やボタンを大きくする（UI の基準の解像度を 1600x900 から MobileReference に小さくする）
/// ・対局画面は、盤を左に寄せて高さいっぱいに、右に1本の欄（駒の情報と対局記録をタブで切り替え）
/// PlayerPrefs の PrefKey が 1 なら PC でもスマホの表示（確かめ用）、-1 ならスマホでも PC の表示
/// </summary>
public static class MobileUI
{
    public const string PrefKey = "c3_mobile_ui";
    public static readonly Vector2Int MobileReference = new Vector2Int(1000, 563);

    /// <summary>スマホの表示か（起動したときに決まる）</summary>
    public static bool Active { get; private set; }

    public enum Tab { Detail, Log }
    public static Tab CurrentTab { get; private set; } = Tab.Detail;
    /// <summary>右の欄のタブが変わった（スマホの表示のときだけ）</summary>
    public static event Action<Tab> TabChanged;

    public static void ShowTab(Tab tab)
    {
        if (!Active || tab == CurrentTab) return;
        CurrentTab = tab;
        if (TabChanged != null) TabChanged(tab);
    }

    /// <summary>スマホの表示では、説明の「クリック」を「タップ」に言い換える</summary>
    public static string Tap(string text)
    {
        return Active && text != null ? text.Replace("クリック", "タップ") : text;
    }

    /// <summary>スマホの端末か（ブラウザの種類で判定。手元で確かめるときは PlayerPrefs で切り替える）</summary>
    public static bool IsMobileDevice()
    {
        return Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        int pref = PlayerPrefs.GetInt(PrefKey, 0);
        Active = !Application.isBatchMode && (pref > 0 || (pref == 0 && IsMobileDevice()));
        CurrentTab = Tab.Detail;
        if (!Active) return;

        // 場面の UIDocument はみな同じ PanelSettings を使っている。その写しを作って基準の解像度だけ小さくし、付け替える
        // （あとから作る画面は、場面の UIDocument の PanelSettings を写して使うので、写しが引き継がれる）
        var docs = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        PanelSettings original = null;
        foreach (var d in docs) if (d.panelSettings != null) { original = d.panelSettings; break; }
        if (original == null) return;
        var mobile = UnityEngine.Object.Instantiate(original);
        mobile.name = original.name + "（スマホ）";
        mobile.referenceResolution = MobileReference;
        foreach (var d in docs) if (d.panelSettings == original) d.panelSettings = mobile;
    }

    // ---- 対局画面の盤の周りの空き（UI の基準の解像度での px） ----

    /// <summary>右の欄（駒の情報・対局記録）の幅</summary>
    public const float SideWidth = 430f;
    /// <summary>右の欄のタブの高さ</summary>
    public const float TabHeight = 44f;
}
