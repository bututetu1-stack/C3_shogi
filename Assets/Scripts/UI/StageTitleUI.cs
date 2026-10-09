using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

/// <summary>ステージ開始時のタイトル（「第三局　中原の戦い」）</summary>
public class StageTitleUI : MonoBehaviour
{
    public static StageTitleUI Instance { get; private set; }

    /// <summary>タイトルが表示されている合計時間</summary>
    public const float Duration = 1.9f;

    private UIDocument uiDocument;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void EnsureUIDocument()
    {
        if (uiDocument != null) return;

        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            uiDocument = gameObject.AddComponent<UIDocument>();
            // 他のUIDocumentと同じPanelSettingsを使う
            UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            for (int i = 0; i < docs.Length; i++)
            {
                if (docs[i] != uiDocument && docs[i].panelSettings != null)
                {
                    uiDocument.panelSettings = docs[i].panelSettings;
                    break;
                }
            }
        }
        uiDocument.sortingOrder = 60; // 他のUIより上
    }

    public void ShowTitle(int stageNumber, string stageName)
    {
        bool boss = IsBossStage(stageNumber);
        Show("第" + UIFactory.Kanji(stageNumber) + "局", stageName, boss ? "― 強敵出現 ―" : null, Palette.Text);
    }

    /// <summary>ステージクリアなどの見出し（大きな文字＋小さな添え書き）</summary>
    public void ShowBanner(string title, string subtitle, Color titleColor)
    {
        Show(subtitle, title, null, titleColor);
    }

    private void Show(string upper, string main, string sub, Color mainColor)
    {
        EnsureUIDocument();
        StopAllCoroutines();
        StartCoroutine(ShowTitleCoroutine(upper, main, sub, mainColor));
    }

    private static bool IsBossStage(int stage)
    {
        return stage == 10 || stage == 12 || stage == 15;
    }

    private IEnumerator ShowTitleCoroutine(string upperText, string mainText, string subText, Color mainColor)
    {
        VisualElement root = UIFactory.SetupRoot(uiDocument);

        var overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0;
        overlay.style.top = 0;
        overlay.style.right = 0;
        overlay.style.bottom = 0;
        overlay.style.justifyContent = Justify.Center;
        overlay.pickingMode = PickingMode.Ignore;
        root.Add(overlay);

        // 横長の帯
        var band = new VisualElement();
        band.style.backgroundColor = new Color(0.06f, 0.045f, 0.035f, 0.94f);
        band.style.borderTopWidth = 2;
        band.style.borderBottomWidth = 2;
        band.style.borderTopColor = Palette.Gold;
        band.style.borderBottomColor = Palette.Gold;
        band.style.paddingTop = 22;
        band.style.paddingBottom = 26;
        band.style.alignItems = Align.Center;
        band.pickingMode = PickingMode.Ignore;
        overlay.Add(band);

        var number = UIFactory.Label(upperText, 26, Palette.Gold, "c3-mincho");
        number.style.letterSpacing = 10;
        band.Add(number);

        var name = UIFactory.Label(mainText, 64, mainColor, "c3-mincho");
        name.style.letterSpacing = 16;
        name.style.marginTop = 2;
        band.Add(name);

        Label sub = null;
        if (!string.IsNullOrEmpty(subText))
        {
            sub = UIFactory.Label(subText, 18, Palette.EnemyLight, "c3-bold");
            sub.style.letterSpacing = 6;
            sub.style.marginTop = 4;
            band.Add(sub);
        }

        // 登場: 帯が縦に開き、文字が横から滑り込む
        const float inTime = 0.3f;
        const float outTime = 0.35f;
        float hold = Duration - inTime - outTime;
        float elapsed = 0f;
        while (elapsed < inTime)
        {
            elapsed += Time.deltaTime;
            float t = Ease.OutCubic(elapsed / inTime);
            band.style.scale = new Scale(new Vector3(1f, Mathf.Max(0.01f, t), 1f));
            name.style.translate = new Translate(Mathf.Lerp(60f, 0f, t), 0f);
            number.style.translate = new Translate(Mathf.Lerp(-60f, 0f, t), 0f);
            SetAlpha(t, band, name, number, sub);
            yield return null;
        }
        band.style.scale = new Scale(Vector3.one);
        name.style.translate = new Translate(0f, 0f);
        number.style.translate = new Translate(0f, 0f);
        SetAlpha(1f, band, name, number, sub);

        yield return new WaitForSeconds(hold);

        // 退場: フェードアウト
        elapsed = 0f;
        while (elapsed < outTime)
        {
            elapsed += Time.deltaTime;
            SetAlpha(1f - elapsed / outTime, band, name, number, sub);
            yield return null;
        }
        root.Clear();
    }

    private static void SetAlpha(float a, params VisualElement[] elements)
    {
        foreach (var e in elements)
            if (e != null) e.style.opacity = a;
    }
}
