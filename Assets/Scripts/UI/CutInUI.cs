using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 画面を斜めに横切る帯に、大きな文字を出すカットイン。
/// 提督の艦隊（「提督 着任」「作戦完了」）、部員の成り（「門人 覚醒」など）、ボスの登場に使う。
/// </summary>
public class CutInUI : MonoBehaviour
{
    public static readonly Color Navy = Palette.Hex(0x14284A);
    public static readonly Color SeaLight = Palette.Hex(0x9FD4FF);

    private static CutInUI instance;
    private UIDocument uiDocument;
    // 前のカットインが終わるまで次を待たせる（李白が3枚まとめて裏返したときなど）
    private static float busyUntil;

    /// <summary>錨（または立ち絵）を主役にしたカットイン（提督の艦隊）</summary>
    public static IEnumerator Play(string title, string subtitle, Sprite portrait, Color bandColor, float duration)
    {
        if (GameSim.Headless) yield break;
        CutInUI ui = Ensure();
        if (ui == null) yield break;

        var art = new VisualElement();
        art.style.width = portrait != null ? 300 : 150;
        art.style.height = portrait != null ? 300 : 150;
        art.style.marginTop = portrait != null ? -60 : 0;
        art.style.backgroundImage = new StyleBackground(portrait != null ? portrait : SpriteFactory.Anchor);
        art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        if (portrait == null && !EffectArt.Has("Anchor")) art.style.unityBackgroundImageTintColor = Palette.Gold;
        yield return ui.Run(title, subtitle, art, bandColor, Palette.Gold, duration);
    }

    /// <summary>駒を主役にしたカットイン（部員の成り・ボスの登場）。accent は帯の縁と小見出しの色</summary>
    public static IEnumerator PlayPiece(PieceData data, bool promoted, string title, string subtitle, Color bandColor, Color accent, float duration)
    {
        if (GameSim.Headless || data == null) yield break;
        CutInUI ui = Ensure();
        if (ui == null) yield break;

        VisualElement art = UIFactory.PieceIcon(data, promoted, 190f);
        art.style.rotate = new Rotate(new Angle(-6f, AngleUnit.Degree));
        yield return ui.Run(title, subtitle, art, bandColor, accent, duration);
    }

    private static CutInUI Ensure()
    {
        if (instance != null) return instance;
        var obj = new GameObject("CutInUI");
        instance = obj.AddComponent<CutInUI>();
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

    private IEnumerator Run(string title, string subtitle, VisualElement art, Color bandColor, Color accent, float duration)
    {
        while (Time.time < busyUntil) yield return null;
        busyUntil = Time.time + duration;
        VisualElement root = UIFactory.SetupRoot(uiDocument);
        root.style.justifyContent = Justify.Center;

        var dim = new VisualElement();
        dim.style.position = Position.Absolute;
        dim.style.left = dim.style.right = dim.style.top = dim.style.bottom = 0;
        dim.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        dim.pickingMode = PickingMode.Ignore;
        root.Add(dim);

        // 斜めの帯（縁は accent の色）
        var band = new VisualElement();
        band.style.position = Position.Absolute;
        band.style.left = Length.Percent(-10);
        band.style.right = Length.Percent(-10);
        band.style.top = Length.Percent(50);
        band.style.height = 210;
        band.style.marginTop = -105;
        band.style.backgroundColor = new Color(bandColor.r, bandColor.g, bandColor.b, 0.96f);
        band.style.borderTopWidth = band.style.borderBottomWidth = 3;
        band.style.borderTopColor = band.style.borderBottomColor = accent;
        band.style.rotate = new Rotate(new Angle(-5f, AngleUnit.Degree));
        band.style.flexDirection = FlexDirection.Row;
        band.style.alignItems = Align.Center;
        band.style.justifyContent = Justify.Center;
        band.pickingMode = PickingMode.Ignore;
        root.Add(band);

        art.style.marginRight = 36;
        art.pickingMode = PickingMode.Ignore;
        band.Add(art);

        var texts = new VisualElement();
        texts.pickingMode = PickingMode.Ignore;
        var titleLabel = UIFactory.Label(title, 76, Color.white, "c3-mincho");
        titleLabel.style.letterSpacing = 18;
        texts.Add(titleLabel);
        var sub = UIFactory.Label(subtitle, 26, Color.Lerp(accent, Color.white, 0.4f), "c3-mincho");
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
            art.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(0.6f, 1f, Ease.OutBack(Mathf.Clamp01(elapsed / inTime))));
            dim.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f * t);
            yield return null;
        }
        band.style.translate = new Translate(0f, 0f);
        texts.style.translate = new Translate(0f, 0f);
        art.style.scale = new Scale(Vector2.one);

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
