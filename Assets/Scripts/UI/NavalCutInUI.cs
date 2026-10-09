using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 提督の艦隊のカットイン（「提督 着任」「作戦完了」など）。
/// 画面を斜めに横切る紺の帯に、立ち絵（あれば）と大きな文字を出す。
/// </summary>
public class NavalCutInUI : MonoBehaviour
{
    public static readonly Color Navy = Palette.Hex(0x14284A);
    public static readonly Color SeaLight = Palette.Hex(0x9FD4FF);

    private static NavalCutInUI instance;
    private UIDocument uiDocument;

    public static IEnumerator Play(string title, string subtitle, Sprite portrait, Color bandColor, float duration)
    {
        if (GameSim.Headless) yield break;
        NavalCutInUI ui = Ensure();
        if (ui == null) yield break;
        yield return ui.Run(title, subtitle, portrait, bandColor, duration);
    }

    private static NavalCutInUI Ensure()
    {
        if (instance != null) return instance;
        var obj = new GameObject("NavalCutInUI");
        instance = obj.AddComponent<NavalCutInUI>();
        instance.uiDocument = obj.AddComponent<UIDocument>();
        foreach (var d in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            if (d != instance.uiDocument && d.panelSettings != null)
            {
                instance.uiDocument.panelSettings = d.panelSettings;
                break;
            }
        }
        instance.uiDocument.sortingOrder = 65;
        return instance;
    }

    private IEnumerator Run(string title, string subtitle, Sprite portrait, Color bandColor, float duration)
    {
        VisualElement root = UIFactory.SetupRoot(uiDocument);
        root.style.justifyContent = Justify.Center;

        var dim = new VisualElement();
        dim.style.position = Position.Absolute;
        dim.style.left = dim.style.right = dim.style.top = dim.style.bottom = 0;
        dim.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        dim.pickingMode = PickingMode.Ignore;
        root.Add(dim);

        // 斜めの帯
        var band = new VisualElement();
        band.style.position = Position.Absolute;
        band.style.left = Length.Percent(-10);
        band.style.right = Length.Percent(-10);
        band.style.top = Length.Percent(50);
        band.style.height = 210;
        band.style.marginTop = -105;
        band.style.backgroundColor = new Color(bandColor.r, bandColor.g, bandColor.b, 0.96f);
        band.style.borderTopWidth = band.style.borderBottomWidth = 3;
        band.style.borderTopColor = band.style.borderBottomColor = Palette.Gold;
        band.style.rotate = new Rotate(new Angle(-5f, AngleUnit.Degree));
        band.style.flexDirection = FlexDirection.Row;
        band.style.alignItems = Align.Center;
        band.style.justifyContent = Justify.Center;
        band.pickingMode = PickingMode.Ignore;
        root.Add(band);

        // 立ち絵（なければ大きな錨）
        var art = new VisualElement();
        art.style.width = portrait != null ? 300 : 150;
        art.style.height = portrait != null ? 300 : 150;
        art.style.marginRight = 36;
        art.style.marginTop = portrait != null ? -60 : 0;
        art.style.backgroundImage = new StyleBackground(portrait != null ? portrait : SpriteFactory.Anchor);
        art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        if (portrait == null && !EffectArt.Has("Anchor")) art.style.unityBackgroundImageTintColor = Palette.Gold;
        art.pickingMode = PickingMode.Ignore;
        band.Add(art);

        var texts = new VisualElement();
        texts.pickingMode = PickingMode.Ignore;
        var titleLabel = UIFactory.Label(title, 76, Color.white, "c3-mincho");
        titleLabel.style.letterSpacing = 18;
        texts.Add(titleLabel);
        var sub = UIFactory.Label(subtitle, 26, Palette.GoldLight, "c3-mincho");
        sub.style.letterSpacing = 8;
        sub.style.marginTop = 2;
        texts.Add(sub);
        band.Add(texts);

        // 左から滑り込み → 止まる → 右へ抜ける
        const float inTime = 0.28f;
        const float outTime = 0.28f;
        float hold = Mathf.Max(0.2f, duration - inTime - outTime);
        float elapsed = 0f;
        while (elapsed < inTime)
        {
            elapsed += Time.deltaTime;
            float t = Ease.OutCubic(elapsed / inTime);
            band.style.translate = new Translate(Length.Percent(Mathf.Lerp(-110f, 0f, t)), 0f);
            texts.style.translate = new Translate(Mathf.Lerp(-80f, 0f, t), 0f);
            dim.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f * t);
            yield return null;
        }
        band.style.translate = new Translate(0f, 0f);
        texts.style.translate = new Translate(0f, 0f);

        // 止まっている間も文字だけゆっくり流す
        elapsed = 0f;
        while (elapsed < hold)
        {
            elapsed += Time.deltaTime;
            texts.style.translate = new Translate(Mathf.Lerp(0f, 24f, elapsed / hold), 0f);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < outTime)
        {
            elapsed += Time.deltaTime;
            float t = Ease.InCubic(elapsed / outTime);
            band.style.translate = new Translate(Length.Percent(Mathf.Lerp(0f, 110f, t)), 0f);
            dim.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f * (1f - t));
            yield return null;
        }
        root.Clear();
    }
}
