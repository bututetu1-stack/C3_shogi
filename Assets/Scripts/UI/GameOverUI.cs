using UnityEngine;
using UnityEngine.UIElements;

/// <summary>決着画面（勝利／敗北と戦績）</summary>
public class GameOverUI : MonoBehaviour
{
    private UIDocument uiDocument;
    private VisualElement root;
    private bool isSubscribed;

    void OnEnable() { TrySubscribe(); }

    void OnDisable()
    {
        if (isSubscribed && GameManager.Instance != null)
            GameManager.Instance.OnGameOver -= ShowGameOver;
        isSubscribed = false;
    }

    private void TrySubscribe()
    {
        if (isSubscribed || GameManager.Instance == null) return;
        GameManager.Instance.OnGameOver += ShowGameOver;
        isSubscribed = true;
    }

    void Start()
    {
        TrySubscribe();
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument != null)
        {
            root = UIFactory.SetupRoot(uiDocument);
            root.style.display = DisplayStyle.None;
        }
    }

    private void ShowGameOver(Team winner)
    {
        if (root == null) return;

        GameManager gm = GameManager.Instance;
        StageManager sm = StageManager.Instance;
        bool won = winner == Team.Player;

        root.Clear();
        root.style.display = DisplayStyle.Flex;
        root.pickingMode = PickingMode.Position;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;
        root.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);

        var panel = UIFactory.Panel();
        panel.style.alignItems = Align.Center;
        panel.style.paddingTop = 30;
        panel.style.paddingBottom = 28;
        panel.style.paddingLeft = 56;
        panel.style.paddingRight = 56;
        panel.style.maxWidth = 720;
        panel.style.borderTopWidth = panel.style.borderBottomWidth = panel.style.borderLeftWidth = panel.style.borderRightWidth = 2;
        Color accent = won ? Palette.GoldLight : Palette.Enemy;
        panel.style.borderTopColor = panel.style.borderBottomColor = panel.style.borderLeftColor = panel.style.borderRightColor = accent;

        var result = UIFactory.Label(won ? "制覇" : "敗北", 84, accent, "c3-mincho");
        result.style.letterSpacing = 24;
        panel.Add(result);

        string sub = won
            ? "全" + UIFactory.Kanji(StageManager.MaxStages) + "局を勝ち抜きました"
            : (sm != null ? "第" + UIFactory.Kanji(sm.currentStage) + "局「" + sm.GetStageName(sm.currentStage) + "」にて敗退" : "");
        var subLabel = UIFactory.Label(sub, 20, Palette.TextSub, "c3-mincho");
        subLabel.style.marginTop = 2;
        subLabel.style.marginBottom = 18;
        panel.Add(subLabel);

        // 戦績
        if (gm != null)
        {
            var stats = new VisualElement();
            stats.style.flexDirection = FlexDirection.Row;
            stats.style.marginBottom = 16;
            stats.Add(StatBox("突破した局", gm.StagesCleared.ToString()));
            stats.Add(StatBox("撃破した駒", gm.TotalKills.ToString()));
            stats.Add(StatBox("総手数", gm.TotalMoves.ToString()));
            panel.Add(stats);

            // 連れていた仲間
            if (gm.OwnedPieces.Count > 0)
            {
                panel.Add(UIFactory.Label("共に戦った仲間", 15, Palette.Gold, "c3-bold"));
                var team = new VisualElement();
                team.style.flexDirection = FlexDirection.Row;
                team.style.flexWrap = Wrap.Wrap;
                team.style.justifyContent = Justify.Center;
                team.style.marginTop = 6;
                team.style.marginBottom = 18;
                foreach (var data in gm.OwnedPieces)
                {
                    var icon = UIFactory.PieceIcon(data, false, 56);
                    icon.style.marginLeft = 3;
                    icon.style.marginRight = 3;
                    team.Add(icon);
                }
                panel.Add(team);
            }

            string bonus = "";
            if (gm.RunBonusATK > 0) bonus += "攻撃+" + gm.RunBonusATK + "  ";
            if (gm.RunBonusDEF > 0) bonus += "防御+" + gm.RunBonusDEF + "  ";
            if (gm.RunBonusHP > 0) bonus += "体力+" + gm.RunBonusHP + "  ";
            if (gm.RunBonusC3HP > 0) bonus += "C3体力+" + gm.RunBonusC3HP;
            if (bonus.Length > 0)
            {
                var b = UIFactory.Label("全軍強化: " + bonus.Trim(), 14, Palette.TextSub);
                b.style.marginBottom = 16;
                panel.Add(b);
            }
        }

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.Add(UIFactory.Button("もう一度挑む", OnRetryClicked, "c3-button--primary", "c3-button--big"));
        buttons.Add(UIFactory.Button("タイトルへ", OnTitleClicked, "c3-button--big"));
        panel.Add(buttons);
        root.Add(panel);

        // ふわっと出す
        panel.style.opacity = 0f;
        panel.style.scale = new Scale(new Vector3(0.92f, 0.92f, 1f));
        panel.schedule.Execute(() =>
        {
            panel.style.transitionProperty = new System.Collections.Generic.List<StylePropertyName> { new StylePropertyName("opacity"), new StylePropertyName("scale") };
            panel.style.transitionDuration = new System.Collections.Generic.List<TimeValue> { new TimeValue(0.35f, TimeUnit.Second), new TimeValue(0.35f, TimeUnit.Second) };
            panel.style.opacity = 1f;
            panel.style.scale = new Scale(Vector3.one);
        }).StartingIn(30);
    }

    private static VisualElement StatBox(string label, string value)
    {
        var box = new VisualElement();
        box.style.alignItems = Align.Center;
        box.style.marginLeft = 18;
        box.style.marginRight = 18;
        box.Add(UIFactory.Label(value, 34, Palette.Text, "c3-bold"));
        box.Add(UIFactory.Label(label, 13, Palette.TextSub));
        return box;
    }

    private void OnRetryClicked()
    {
        GameManager.SkipTitleOnce = true;
        ReloadScene();
    }

    private void OnTitleClicked()
    {
        GameManager.SkipTitleOnce = false;
        ReloadScene();
    }

    private static void ReloadScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}
