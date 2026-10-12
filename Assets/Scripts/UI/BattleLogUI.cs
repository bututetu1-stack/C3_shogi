using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>画面左の対局ログ</summary>
public class BattleLogUI : MonoBehaviour
{
    private static BattleLogUI instance;
    /// <summary>対局記録（自動プレイの Headless 中は null）</summary>
    public static BattleLogUI Instance { get { return GameSim.Headless ? null : instance; } }

    private ScrollView scrollView;
    private readonly List<Label> logEntries = new List<Label>();
    private const int MaxEntries = 80;

    public static string ColorName(string name, Team team)
    {
        return "<color=" + Palette.ToHex(Palette.TeamLight(team)) + ">" + name + "</color>";
    }

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(this); return; }
    }

    void Start()
    {
        UIDocument uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;
        VisualElement root = UIFactory.SetupRoot(uiDocument);

        panel = UIFactory.Panel();
        panel.style.position = Position.Absolute;
        if (MobileUI.Active)
        {
            // スマホの表示: 右の欄で、駒の情報とタブで切り替える
            panel.style.right = 10;
            panel.style.top = CameraFitter.TopReserve + MobileUI.TabHeight;
            panel.style.width = MobileUI.SideWidth;
            MobileUI.TabChanged += OnTab;
            OnTab(MobileUI.CurrentTab);
        }
        else
        {
            panel.style.left = 16;
            panel.style.width = CameraFitter.LeftReserve - 32;
            panel.style.top = CameraFitter.TopReserve;
        }
        panel.style.bottom = CameraFitter.BottomReserve;
        panel.style.paddingLeft = 10;
        panel.style.paddingRight = 6;
        // スクロールできるようにクリックを受け取る（盤とは重ならない位置）
        panel.pickingMode = PickingMode.Position;
        root.Add(panel);

        panel.Add(UIFactory.PanelTitle("対局記録"));

        scrollView = UIFactory.Scroll();
        scrollView.style.flexGrow = 1;
        scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        panel.Add(scrollView);
    }

    private VisualElement panel;

    private void OnTab(MobileUI.Tab tab)
    {
        if (panel != null) panel.style.display = tab == MobileUI.Tab.Log ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void OnDestroy()
    {
        MobileUI.TabChanged -= OnTab;
    }

    public void AddLog(string message)
    {
        if (scrollView == null) return;

        var label = new WrappedLabel(message);
        label.enableRichText = true;
        label.style.fontSize = 17;
        label.style.color = Palette.Text;
        label.style.paddingTop = 4;
        label.style.paddingBottom = 4;
        label.style.borderBottomWidth = 1;
        label.style.borderBottomColor = new Color(1f, 1f, 1f, 0.05f);
        label.pickingMode = PickingMode.Ignore;

        // 新しい行は少し光らせてから落ち着かせる
        label.style.backgroundColor = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.18f);
        label.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("background-color") };
        label.style.transitionDuration = new List<TimeValue> { new TimeValue(0.8f, TimeUnit.Second) };
        label.schedule.Execute(() => label.style.backgroundColor = new Color(0, 0, 0, 0)).StartingIn(50);

        scrollView.Add(label);
        logEntries.Add(label);

        while (logEntries.Count > MaxEntries)
        {
            Label old = logEntries[0];
            logEntries.RemoveAt(0);
            scrollView.Remove(old);
        }

        // 最新の行までスクロール（その前にログが消されていたら何もしない）
        scrollView.schedule.Execute(() =>
        {
            if (label.parent == scrollView.contentContainer)
                scrollView.ScrollTo(label);
        }).StartingIn(16);
    }

    public void ClearLog()
    {
        if (scrollView == null) return;
        scrollView.Clear();
        logEntries.Clear();
    }
}
