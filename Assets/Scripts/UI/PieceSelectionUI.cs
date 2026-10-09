using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>ステージ開始前に仲間（駒）を1つ選ぶ画面</summary>
public class PieceSelectionUI : MonoBehaviour
{
    private UIDocument uiDocument;
    private VisualElement root;
    private List<PieceData> currentChoices;
    private System.Action<PieceData> onPieceSelected;
    private PieceData selectedPiece;
    private bool showingPromoted;
    private readonly List<VisualElement> cards = new List<VisualElement>();
    private VisualElement detailPanel;
    private Button confirmButton;

    void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private bool EnsureRoot()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null || uiDocument.rootVisualElement == null) return false;
        root = UIFactory.SetupRoot(uiDocument);
        return true;
    }

    void Start()
    {
        if (root == null && EnsureRoot()) root.style.display = DisplayStyle.None;
    }

    public void ShowSelection(List<PieceData> choices, System.Action<PieceData> callback)
    {
        if (!EnsureRoot())
        {
            // UIが使えない場合でも進行が止まらないようにする
            if (callback != null) callback(null);
            return;
        }

        currentChoices = choices;
        onPieceSelected = callback;
        selectedPiece = null;
        showingPromoted = false;
        cards.Clear();

        root.style.display = DisplayStyle.Flex;
        root.style.backgroundColor = new Color(0.03f, 0.025f, 0.02f, 0.95f);
        root.pickingMode = PickingMode.Position;   // 盤へのクリックを遮る

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1;
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.contentContainer.style.alignItems = Align.Center;
        scroll.contentContainer.style.paddingTop = CameraFitter.TopReserve + 8;
        scroll.contentContainer.style.paddingBottom = 36;
        root.Add(scroll);

        // 見出し
        StageManager sm = StageManager.Instance;
        if (sm != null)
        {
            var stage = UIFactory.Label("第" + UIFactory.Kanji(sm.currentStage) + "局「" + sm.GetStageName(sm.currentStage) + "」を前に", 18, Palette.Gold, "c3-mincho");
            stage.style.marginBottom = 4;
            scroll.Add(stage);
        }
        var title = UIFactory.Label("仲間をひとり選んでください", 34, Palette.Text, "c3-mincho");
        title.style.letterSpacing = 4;
        title.style.marginBottom = 26;
        scroll.Add(title);

        // カード
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.justifyContent = Justify.Center;
        row.style.marginBottom = 20;
        foreach (var piece in choices)
            row.Add(CreateCard(piece));
        scroll.Add(row);

        // 選んだ駒の詳しい説明
        detailPanel = UIFactory.Panel();
        detailPanel.style.width = Length.Percent(90);
        detailPanel.style.maxWidth = 820;
        detailPanel.style.flexDirection = FlexDirection.Row;
        detailPanel.style.display = DisplayStyle.None;
        scroll.Add(detailPanel);

        confirmButton = UIFactory.Button("この仲間で挑む", OnConfirm, "c3-button--primary", "c3-button--big");
        confirmButton.style.marginTop = 22;
        confirmButton.SetEnabled(false);
        scroll.Add(confirmButton);
    }

    private VisualElement CreateCard(PieceData piece)
    {
        var card = new VisualElement();
        card.AddToClassList("c3-card");

        card.Add(UIFactory.PieceIcon(piece, false, piece.portrait != null ? 150 : 110));

        var name = UIFactory.Label(piece.pieceName, 22, Palette.Text, "c3-mincho");
        name.style.marginTop = 8;
        card.Add(name);

        var chip = UIFactory.Chip(Palette.RarityName(piece.rarity), Palette.RarityLabel(piece.rarity));
        chip.style.marginTop = 4;
        card.Add(chip);

        var stats = new VisualElement();
        stats.style.flexDirection = FlexDirection.Row;
        stats.style.marginTop = 8;
        stats.Add(StatText("攻", piece.baseATK.ToString(), Palette.ATK));
        stats.Add(StatText("防", piece.baseDEF.ToString(), Palette.DEF));
        stats.Add(StatText("体", UIFactory.FormatHP(piece.baseHP), Palette.HP));
        card.Add(stats);

        var grid = UIFactory.MoveGrid(piece.moveDirections, false, 11f, 3);
        grid.style.marginTop = 10;
        card.Add(grid);

        PieceData captured = piece;
        card.RegisterCallback<ClickEvent>(evt => SelectCard(captured));
        cards.Add(card);
        return card;
    }

    private static VisualElement StatText(string label, string value, Color color)
    {
        var box = new VisualElement();
        box.style.flexDirection = FlexDirection.Row;
        box.style.alignItems = Align.Center;
        box.style.marginLeft = 6;
        box.style.marginRight = 6;
        box.Add(UIFactory.Label(label, 13, color, "c3-bold"));
        var v = UIFactory.Label(value, 18, Palette.Text, "c3-bold");
        v.style.marginLeft = 3;
        box.Add(v);
        return box;
    }

    private void SelectCard(PieceData piece)
    {
        selectedPiece = piece;
        showingPromoted = false;

        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayMoveEffect();

        for (int i = 0; i < cards.Count; i++)
            cards[i].EnableInClassList("c3-card--selected", i < currentChoices.Count && currentChoices[i] == piece);

        RenderDetailPanel(piece, false);
        detailPanel.style.display = DisplayStyle.Flex;
        confirmButton.SetEnabled(true);
    }

    private void RenderDetailPanel(PieceData piece, bool promoted)
    {
        detailPanel.Clear();
        promoted = promoted && piece.canPromote;

        string fullName = promoted ? piece.promotedName : piece.pieceName;
        string desc = promoted ? piece.promotedDescription : piece.description;
        int atk = promoted ? piece.promotedATK : piece.baseATK;
        int def = promoted ? piece.promotedDEF : piece.baseDEF;
        int hp = promoted ? piece.promotedHP : piece.baseHP;

        // 左: 名前・ステータス・説明
        var left = new VisualElement();
        left.style.flexGrow = 1;
        left.style.flexShrink = 1;
        left.style.marginRight = 16;

        var head = new VisualElement();
        head.style.flexDirection = FlexDirection.Row;
        head.style.alignItems = Align.Center;
        head.style.marginBottom = 10;
        head.Add(UIFactory.Label(fullName, 24, Palette.Text, "c3-mincho"));
        if (promoted)
        {
            var chip = UIFactory.Chip("成り", Palette.EnemyLight);
            chip.style.marginLeft = 8;
            head.Add(chip);
        }
        left.Add(head);
        left.Add(UIFactory.StatRow(atk, def, UIFactory.FormatHP(hp)));

        var descLabel = UIFactory.Label(KinsokuHelper.Apply(desc), 15, Palette.TextSub);
        descLabel.style.whiteSpace = WhiteSpace.Normal;
        descLabel.style.marginTop = 12;
        left.Add(descLabel);

        if (piece.canPromote)
        {
            bool nextState = !promoted;
            var toggle = UIFactory.Button(promoted ? "成る前を見る" : "成りを見る", () =>
            {
                showingPromoted = nextState;
                RenderDetailPanel(piece, nextState);
            });
            toggle.style.alignSelf = Align.FlexStart;
            toggle.style.marginTop = 12;
            toggle.style.marginLeft = 0;
            left.Add(toggle);
        }
        detailPanel.Add(left);

        // 右: 動き
        var right = new VisualElement();
        right.style.alignItems = Align.Center;
        right.Add(UIFactory.Label(promoted ? "動き（成り）" : "動き", 15, Palette.Gold, "c3-bold"));
        var grid = UIFactory.MoveGrid(UIFactory.MovesOf(piece, promoted), false, 22f);
        grid.style.marginTop = 6;
        right.Add(grid);
        detailPanel.Add(right);
    }

    private void OnConfirm()
    {
        if (selectedPiece == null) return;
        root.style.display = DisplayStyle.None;
        root.pickingMode = PickingMode.Ignore;
        root.Clear();
        var callback = onPieceSelected;
        onPieceSelected = null;
        if (callback != null) callback(selectedPiece);
    }
}
