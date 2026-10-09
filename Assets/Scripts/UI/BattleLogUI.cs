using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>画面左の対局ログ</summary>
public class BattleLogUI : MonoBehaviour
{
    public static BattleLogUI Instance { get; private set; }

    private ScrollView scrollView;
    private readonly List<Label> logEntries = new List<Label>();
    private const int MaxEntries = 80;

    public static string ColorName(string name, Team team)
    {
        return "<color=" + Palette.ToHex(Palette.TeamLight(team)) + ">" + name + "</color>";
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    void Start()
    {
        UIDocument uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;
        VisualElement root = UIFactory.SetupRoot(uiDocument);

        var panel = UIFactory.Panel();
        panel.style.position = Position.Absolute;
        panel.style.left = 16;
        panel.style.top = CameraFitter.TopReserve;
        panel.style.bottom = CameraFitter.BottomReserve;
        panel.style.width = CameraFitter.LeftReserve - 32;
        panel.style.paddingLeft = 10;
        panel.style.paddingRight = 6;
        // スクロールできるようにクリックを受け取る（盤とは重ならない位置）
        panel.pickingMode = PickingMode.Position;
        root.Add(panel);

        panel.Add(UIFactory.PanelTitle("対局記録"));

        scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.style.flexGrow = 1;
        scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        panel.Add(scrollView);
    }

    public void AddLog(string message)
    {
        if (scrollView == null) return;

        var label = new Label(KinsokuHelper.Apply(message));
        label.enableRichText = true;
        label.style.fontSize = 15;
        label.style.color = Palette.Text;
        label.style.whiteSpace = WhiteSpace.Normal;
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

        // 最新の行までスクロール
        scrollView.schedule.Execute(() => scrollView.ScrollTo(label)).StartingIn(16);
    }

    public void ClearLog()
    {
        if (scrollView == null) return;
        scrollView.Clear();
        logEntries.Clear();
    }
}
