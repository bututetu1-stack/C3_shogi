using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class BattleLogUI : MonoBehaviour
{
    public static BattleLogUI Instance { get; private set; }

    private UIDocument uiDocument;
    private VisualElement logPanel;
    private ScrollView scrollView;
    private List<Label> logEntries = new List<Label>();
    private static UnityEngine.TextCore.Text.FontAsset sdfFont;
    private const int MAX_ENTRIES = 50;

    private static readonly string PlayerColorHex = "#66B2FF";
    private static readonly string EnemyColorHex = "#FF6666";

    public static string ColorName(string name, Team team)
    {
        string hex = (team == Team.Player) ? PlayerColorHex : EnemyColorHex;
        return "<color=" + hex + ">" + name + "</color>";
    }

    private static UnityEngine.TextCore.Text.FontAsset GetSDFFont()
    {
        if (sdfFont == null)
            sdfFont = Resources.Load<UnityEngine.TextCore.Text.FontAsset>("NotoSansJP-SDF");
        return sdfFont;
    }

    private void ApplyFont(VisualElement elem)
    {
        var font = GetSDFFont();
        if (font != null)
            elem.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        var root = uiDocument.rootVisualElement;
        root.Clear(); // UXML由来の子要素を除去
        root.pickingMode = PickingMode.Ignore;
        root.style.width = Length.Percent(100);
        root.style.height = Length.Percent(100);

        logPanel = new VisualElement();
        logPanel.style.position = Position.Absolute;
        logPanel.style.left = 10;
        logPanel.style.top = 100;
        logPanel.style.bottom = 10;
        logPanel.style.width = 320;
        logPanel.style.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.75f);
        logPanel.style.borderTopLeftRadius = 8;
        logPanel.style.borderTopRightRadius = 8;
        logPanel.style.borderBottomLeftRadius = 8;
        logPanel.style.borderBottomRightRadius = 8;
        logPanel.pickingMode = PickingMode.Ignore;
        logPanel.style.overflow = Overflow.Hidden;

        // タイトル
        var title = new Label("Battle Log");
        title.style.fontSize = 20;
        title.style.color = new Color(0.7f, 0.7f, 0.7f);
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.style.paddingTop = 6;
        title.style.paddingBottom = 4;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.pickingMode = PickingMode.Ignore;
        ApplyFont(title);
        logPanel.Add(title);

        scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.style.flexGrow = 1;
        scrollView.style.paddingLeft = 8;
        scrollView.style.paddingRight = 8;
        scrollView.style.paddingBottom = 8;
        scrollView.pickingMode = PickingMode.Ignore;
        ApplyFont(scrollView);
        logPanel.Add(scrollView);

        root.Add(logPanel);
    }

    public void AddLog(string message)
    {
        if (scrollView == null) return;

        var label = new Label(KinsokuHelper.Apply(message));
        label.style.fontSize = 20;
        label.style.color = new Color(0.85f, 0.85f, 0.85f);
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginBottom = 2;
        label.pickingMode = PickingMode.Ignore;
        label.enableRichText = true;
        ApplyFont(label);

        scrollView.Add(label);
        logEntries.Add(label);

        // 最大件数超過時に古いエントリを削除
        while (logEntries.Count > MAX_ENTRIES)
        {
            Label old = logEntries[0];
            logEntries.RemoveAt(0);
            scrollView.Remove(old);
        }

        // 自動スクロール
        scrollView.schedule.Execute(() =>
        {
            scrollView.scrollOffset = new Vector2(0, float.MaxValue);
        });
    }

    public void ClearLog()
    {
        if (scrollView == null) return;
        scrollView.Clear();
        logEntries.Clear();
    }
}
