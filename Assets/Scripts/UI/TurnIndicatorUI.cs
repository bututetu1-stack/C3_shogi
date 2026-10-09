using UnityEngine;
using UnityEngine.UIElements;

/// <summary>画面上部のHUD（第N局・局名・手番・手数・パス／投了）</summary>
public class TurnIndicatorUI : MonoBehaviour
{
    private Label stageNumberLabel;
    private Label stageNameLabel;
    private VisualElement turnBadge;
    private Label turnLabel;
    private Label moveCountLabel;
    private VisualElement buttonContainer;
    private bool isSubscribed;

    void Start()
    {
        UIDocument uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        // ボタンが確実にクリック可能になるよう、他UIより上に配置
        uiDocument.sortingOrder = 30;
        VisualElement root = UIFactory.SetupRoot(uiDocument);

        var bar = new VisualElement();
        bar.style.position = Position.Absolute;
        bar.style.top = 14;
        bar.style.left = 0;
        bar.style.right = 0;
        bar.style.flexDirection = FlexDirection.Row;
        bar.style.justifyContent = Justify.Center;
        bar.style.alignItems = Align.Center;
        bar.pickingMode = PickingMode.Ignore;
        root.Add(bar);

        // 局の表示
        var stagePlate = UIFactory.Panel();
        stagePlate.style.flexDirection = FlexDirection.Row;
        stagePlate.style.alignItems = Align.Center;
        stagePlate.style.paddingTop = 6;
        stagePlate.style.paddingBottom = 6;
        stagePlate.style.paddingLeft = 18;
        stagePlate.style.paddingRight = 18;
        stagePlate.pickingMode = PickingMode.Ignore;
        stageNumberLabel = UIFactory.Label("第一局", 16, Palette.Gold, "c3-mincho");
        stageNumberLabel.style.marginRight = 12;
        stageNameLabel = UIFactory.Label("", 22, Palette.Text, "c3-mincho");
        stagePlate.Add(stageNumberLabel);
        stagePlate.Add(stageNameLabel);
        bar.Add(stagePlate);

        // 手番
        turnBadge = new VisualElement();
        turnBadge.style.marginLeft = 12;
        turnBadge.style.paddingTop = 7;
        turnBadge.style.paddingBottom = 7;
        turnBadge.style.paddingLeft = 18;
        turnBadge.style.paddingRight = 18;
        turnBadge.style.borderTopLeftRadius = turnBadge.style.borderTopRightRadius =
            turnBadge.style.borderBottomLeftRadius = turnBadge.style.borderBottomRightRadius = 20;
        turnBadge.style.flexDirection = FlexDirection.Row;
        turnBadge.style.alignItems = Align.Center;
        turnBadge.pickingMode = PickingMode.Ignore;
        turnLabel = UIFactory.Label("", 18, Color.white, "c3-bold");
        moveCountLabel = UIFactory.Label("", 13, new Color(1f, 1f, 1f, 0.75f));
        moveCountLabel.style.marginLeft = 10;
        turnBadge.Add(turnLabel);
        turnBadge.Add(moveCountLabel);
        bar.Add(turnBadge);

        // パス・投了
        buttonContainer = new VisualElement();
        buttonContainer.style.flexDirection = FlexDirection.Row;
        buttonContainer.style.marginLeft = 12;
        buttonContainer.pickingMode = PickingMode.Ignore;
        buttonContainer.Add(UIFactory.Button("パス", OnPassClicked));
        buttonContainer.Add(UIFactory.Button("投了", OnResignClicked, "c3-button--danger"));
        bar.Add(buttonContainer);

        SubscribeEvents();
    }

    void Update()
    {
        // GameManager生成前にStartが走った場合の遅延登録
        if (!isSubscribed) SubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (isSubscribed || GameManager.Instance == null || turnLabel == null) return;
        GameManager.Instance.OnTurnChanged += UpdateTurnDisplay;
        GameManager.Instance.OnPhaseChanged += OnPhaseChanged;
        isSubscribed = true;
        UpdateTurnDisplay(GameManager.Instance.currentTurn);
    }

    void OnDisable()
    {
        if (isSubscribed && GameManager.Instance != null)
        {
            GameManager.Instance.OnTurnChanged -= UpdateTurnDisplay;
            GameManager.Instance.OnPhaseChanged -= OnPhaseChanged;
        }
        isSubscribed = false;
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        if (GameManager.Instance != null)
            UpdateTurnDisplay(GameManager.Instance.currentTurn);
    }

    private void UpdateTurnDisplay(Team team)
    {
        if (turnLabel == null || GameManager.Instance == null) return;

        StageManager sm = StageManager.Instance;
        if (sm != null)
        {
            stageNumberLabel.text = "第" + UIFactory.Kanji(sm.currentStage) + "局";
            stageNameLabel.text = sm.GetStageName(sm.currentStage);
        }

        GamePhase phase = GameManager.Instance.currentPhase;
        bool battle = phase == GamePhase.Battle;
        turnBadge.style.display = battle ? DisplayStyle.Flex : DisplayStyle.None;

        if (team == Team.Player)
        {
            turnLabel.text = "あなたの番";
            turnBadge.style.backgroundColor = new Color(Palette.Player.r, Palette.Player.g, Palette.Player.b, 0.9f);
        }
        else
        {
            turnLabel.text = "相手の番";
            turnBadge.style.backgroundColor = new Color(Palette.Enemy.r, Palette.Enemy.g, Palette.Enemy.b, 0.9f);
        }
        moveCountLabel.text = GameManager.Instance.MoveCount + "手目";

        buttonContainer.style.display = (battle && team == Team.Player) ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnPassClicked()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.currentTurn != Team.Player || gm.currentPhase != GamePhase.Battle || gm.IsTurnProcessing) return;
        if (InputManager.Instance != null)
        {
            if (InputManager.Instance.IsBusy) return;
            InputManager.Instance.ClearSelection();
        }
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("あなた", Team.Player) + " はパスした");
        gm.EndTurn();
    }

    private void OnResignClicked()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.currentPhase != GamePhase.Battle || gm.IsTurnProcessing) return;
        if (InputManager.Instance != null && InputManager.Instance.IsBusy) return;
        gm.Resign();
    }
}
