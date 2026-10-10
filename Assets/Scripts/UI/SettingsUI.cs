using UnityEngine;
using UnityEngine.UIElements;

/// <summary>設定（BGM と効果音の音量）。タイトル画面と、対局中の上部のボタンから開く</summary>
public class SettingsUI : MonoBehaviour
{
    private static SettingsUI instance;
    private UIDocument uiDocument;
    private VisualElement overlay;

    public static bool IsOpen { get { return instance != null && instance.overlay != null; } }

    public static void Open()
    {
        Ensure().Show();
    }

    public static void Close()
    {
        if (instance != null) instance.Hide();
    }

    private static SettingsUI Ensure()
    {
        if (instance != null) return instance;
        var obj = new GameObject("SettingsUI");
        instance = obj.AddComponent<SettingsUI>();
        instance.uiDocument = obj.AddComponent<UIDocument>();
        foreach (var d in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            if (d != instance.uiDocument && d.panelSettings != null)
            {
                instance.uiDocument.panelSettings = d.panelSettings;
                break;
            }
        }
        instance.uiDocument.sortingOrder = 96;   // タイトル画面（90）・駒一覧（95）より上
        return instance;
    }

    private void Show()
    {
        VisualElement root = UIFactory.SetupRoot(uiDocument);
        root.Clear();

        overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = overlay.style.right = overlay.style.top = overlay.style.bottom = 0;
        overlay.style.backgroundColor = new Color(0.03f, 0.025f, 0.02f, 0.85f);
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.pickingMode = PickingMode.Position;   // 盤へのクリックを遮る
        root.Add(overlay);

        var panel = UIFactory.Panel();
        panel.style.width = 640;
        panel.style.paddingLeft = panel.style.paddingRight = 36;
        panel.style.paddingTop = panel.style.paddingBottom = 28;
        panel.style.backgroundColor = new Color(0.075f, 0.065f, 0.055f, 1f);   // 後ろのタイトルの文字を透かさない
        overlay.Add(panel);

        var title = UIFactory.Label("設定", 32, Palette.GoldLight, "c3-mincho");
        title.style.letterSpacing = 6;
        title.style.marginBottom = 20;
        panel.Add(title);

        panel.Add(VolumeRow("BGM", SoundSettings.Bgm, v => SoundSettings.Bgm = v, false));
        panel.Add(VolumeRow("効果音", SoundSettings.Se, v => SoundSettings.Se = v, true));

        var close = UIFactory.Button("閉じる", Hide);
        close.style.alignSelf = Align.Center;
        close.style.marginTop = 24;
        close.style.width = 200;
        panel.Add(close);
    }

    /// <summary>音量の行（名前・スライダー・%）。効果音は指を離したときに試しに鳴らす</summary>
    private static VisualElement VolumeRow(string name, float value, System.Action<float> apply, bool playSample)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 16;

        var label = UIFactory.Label(name, 22, Palette.Text, "c3-bold");
        label.style.width = 110;
        row.Add(label);

        var slider = new Slider(0f, 1f);
        slider.value = value;
        slider.style.flexGrow = 1;
        row.Add(slider);

        var percent = UIFactory.Label(Mathf.RoundToInt(value * 100f) + "%", 20, Palette.TextSub, "c3-bold");
        percent.style.width = 70;
        percent.style.unityTextAlign = TextAnchor.MiddleRight;
        row.Add(percent);

        slider.RegisterValueChangedCallback(evt =>
        {
            apply(evt.newValue);
            percent.text = Mathf.RoundToInt(evt.newValue * 100f) + "%";
        });
        if (playSample)
            slider.RegisterCallback<PointerCaptureOutEvent>(_ => { if (BattleEffects.Instance != null) BattleEffects.Instance.PlayClick(); });
        return row;
    }

    private void Hide()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            uiDocument.rootVisualElement.Clear();
            uiDocument.rootVisualElement.pickingMode = PickingMode.Ignore;
        }
        overlay = null;
    }
}
