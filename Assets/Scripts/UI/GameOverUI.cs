using UnityEngine;
using UnityEngine.UIElements;

/// <summary>決着画面（勝利／敗北）</summary>
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

        bool won = winner == Team.Player;
        root.Clear();
        root.style.display = DisplayStyle.Flex;
        root.pickingMode = PickingMode.Position;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;
        root.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);

        var panel = UIFactory.Panel();
        panel.style.alignItems = Align.Center;
        panel.style.paddingTop = 36;
        panel.style.paddingBottom = 32;
        panel.style.paddingLeft = 72;
        panel.style.paddingRight = 72;
        panel.style.borderTopWidth = panel.style.borderBottomWidth = panel.style.borderLeftWidth = panel.style.borderRightWidth = 2;
        Color accent = won ? Palette.GoldLight : Palette.Enemy;
        panel.style.borderTopColor = panel.style.borderBottomColor = panel.style.borderLeftColor = panel.style.borderRightColor = accent;

        var result = UIFactory.Label(won ? "勝利" : "敗北", 84, accent, "c3-mincho");
        result.style.letterSpacing = 24;
        panel.Add(result);

        StageManager sm = StageManager.Instance;
        string sub = won
            ? "全" + UIFactory.Kanji(StageManager.MaxStages) + "局を制覇しました"
            : (sm != null ? "第" + UIFactory.Kanji(sm.currentStage) + "局「" + sm.GetStageName(sm.currentStage) + "」にて敗退" : "");
        var subLabel = UIFactory.Label(sub, 20, Palette.TextSub, "c3-mincho");
        subLabel.style.marginTop = 4;
        subLabel.style.marginBottom = 26;
        panel.Add(subLabel);

        panel.Add(UIFactory.Button("もう一度挑む", OnRetryClicked, "c3-button--primary", "c3-button--big"));
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

    private void OnRetryClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}
