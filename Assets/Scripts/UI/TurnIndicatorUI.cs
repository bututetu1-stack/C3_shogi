using UnityEngine;
using UnityEngine.UIElements;

public class TurnIndicatorUI : MonoBehaviour
{
    private UIDocument uiDocument;
    private Label turnLabel;
    private VisualElement buttonContainer;
    private Button passButton;
    private Button resignButton;
    private static UnityEngine.TextCore.Text.FontAsset sdfFont;

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

    void Start()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        // ボタンが確実にクリック可能になるよう、他UIより上に配置
        uiDocument.sortingOrder = 30;

        var root = uiDocument.rootVisualElement;
        root.Clear(); // UXML由来の子要素を除去（PickingMode.Position干渉防止）
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.style.width = Length.Percent(100);
        root.style.height = Length.Percent(100);
        root.pickingMode = PickingMode.Ignore;

        turnLabel = new Label("Your Turn");
        turnLabel.style.position = Position.Absolute;
        turnLabel.style.top = 10;
        turnLabel.style.left = 0;
        turnLabel.style.right = 0;
        turnLabel.style.fontSize = 24;
        turnLabel.style.color = Color.white;
        turnLabel.style.unityTextAlign = TextAnchor.UpperCenter;
        turnLabel.pickingMode = PickingMode.Ignore;
        ApplyFont(turnLabel);
        root.Add(turnLabel);

        // ボタンコンテナ
        buttonContainer = new VisualElement();
        buttonContainer.style.position = Position.Absolute;
        buttonContainer.style.top = 50;
        buttonContainer.style.left = 0;
        buttonContainer.style.right = 0;
        buttonContainer.style.height = 50;
        buttonContainer.style.flexDirection = FlexDirection.Row;
        buttonContainer.style.justifyContent = Justify.Center;
        buttonContainer.style.alignItems = Align.Center;
        buttonContainer.pickingMode = PickingMode.Ignore;

        passButton = CreateButton("\u30D1\u30B9", new Color(0.3f, 0.45f, 0.55f));
        passButton.clicked += OnPassClicked;
        buttonContainer.Add(passButton);

        resignButton = CreateButton("\u6295\u4E86", new Color(0.65f, 0.25f, 0.25f));
        resignButton.clicked += OnResignClicked;
        buttonContainer.Add(resignButton);

        root.Add(buttonContainer);

        // イベント登録（遅延対応）
        SubscribeEvents();
    }

    void Update()
    {
        // GameManager生成前にStartが走った場合の遅延登録
        if (!isSubscribed) SubscribeEvents();
    }

    private bool isSubscribed;

    private void SubscribeEvents()
    {
        if (isSubscribed) return;
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnTurnChanged += UpdateTurnDisplay;
        GameManager.Instance.OnPhaseChanged += OnPhaseChanged;
        isSubscribed = true;
        // 初期表示更新（ステージ番号含む）
        UpdateTurnDisplay(GameManager.Instance.currentTurn);
    }

    void OnDisable()
    {
        if (isSubscribed && GameManager.Instance != null)
        {
            GameManager.Instance.OnTurnChanged -= UpdateTurnDisplay;
            GameManager.Instance.OnPhaseChanged -= OnPhaseChanged;
            isSubscribed = false;
        }
    }

    private Button CreateButton(string text, Color bgColor)
    {
        var btn = new Button();
        btn.text = text;
        btn.style.fontSize = 18;
        btn.style.paddingTop = 8;
        btn.style.paddingBottom = 8;
        btn.style.paddingLeft = 24;
        btn.style.paddingRight = 24;
        btn.style.marginLeft = 8;
        btn.style.marginRight = 8;
        btn.style.backgroundColor = bgColor;
        btn.style.color = Color.white;
        btn.style.borderTopLeftRadius = 8;
        btn.style.borderTopRightRadius = 8;
        btn.style.borderBottomLeftRadius = 8;
        btn.style.borderBottomRightRadius = 8;
        btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        btn.pickingMode = PickingMode.Position;
        ApplyFont(btn);
        return btn;
    }

    private void UpdateTurnDisplay(Team team)
    {
        if (turnLabel == null) return;

        string stagePrefix = "";
        if (StageManager.Instance != null)
            stagePrefix = "Stage " + StageManager.Instance.currentStage + "/" + StageManager.Instance.maxStages + " - ";

        if (team == Team.Player)
        {
            turnLabel.text = stagePrefix + "Your Turn";
            turnLabel.style.color = new Color(0.3f, 0.7f, 1f);
        }
        else
        {
            turnLabel.text = stagePrefix + "Enemy Turn";
            turnLabel.style.color = new Color(1f, 0.4f, 0.4f);
        }

        UpdateButtonVisibility(team);
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        if (GameManager.Instance != null)
            UpdateTurnDisplay(GameManager.Instance.currentTurn);
    }

    private void UpdateButtonVisibility(Team team)
    {
        if (buttonContainer == null) return;
        bool show = (team == Team.Player) &&
                    (GameManager.Instance != null && GameManager.Instance.currentPhase == GamePhase.Battle);
        buttonContainer.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnPassClicked()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.currentTurn != Team.Player) return;
        if (GameManager.Instance.currentPhase != GamePhase.Battle) return;
        if (GameManager.Instance.IsTurnProcessing) return;

        if (InputManager.Instance != null)
            InputManager.Instance.ClearSelection();
        GameManager.Instance.EndTurn();
    }

    private void OnResignClicked()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.currentPhase != GamePhase.Battle) return;
        if (GameManager.Instance.IsTurnProcessing) return;

        GameManager.Instance.Resign();
    }
}
