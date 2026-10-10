using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>
/// ステージ開始前に仲間（駒）か全軍強化を1つ選ぶ画面。
/// カードには絵と名前だけを出し、選んだカードの説明は画面下部に大きく表示する。
/// </summary>
public class PieceSelectionUI : MonoBehaviour
{
    private UIDocument uiDocument;
    private VisualElement root;
    private List<DraftOption> currentChoices;
    private System.Action<DraftOption> onPieceSelected;
    private DraftOption selectedOption;
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

    /// <param name="onReroll">引き直しボタンを押したとき（null なら引き直し不可）</param>
    /// <summary>pickCount が2以上なら「2枚のうち1枚目」のように見出しに出す</summary>
    public void ShowSelection(List<DraftOption> choices, System.Action<DraftOption> callback, System.Action onReroll = null, int rerollsLeft = 0,
        int pickIndex = 1, int pickCount = 1)
    {
        if (!EnsureRoot())
        {
            // UIが使えない場合でも進行が止まらないようにする
            if (callback != null) callback(null);
            return;
        }

        currentChoices = choices;
        onPieceSelected = callback;
        selectedOption = null;
        showingPromoted = false;
        cards.Clear();

        root.style.display = DisplayStyle.Flex;
        root.style.backgroundColor = new Color(0.03f, 0.025f, 0.02f, 0.95f);
        root.pickingMode = PickingMode.Position;   // 盤へのクリックを遮る

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1;
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.contentContainer.style.alignItems = Align.Center;
        scroll.contentContainer.style.paddingTop = 18;
        scroll.contentContainer.style.paddingBottom = 18;
        root.Add(scroll);

        // 見出し
        StageManager sm = StageManager.Instance;
        if (sm != null)
        {
            var stage = UIFactory.Label("第" + UIFactory.Kanji(sm.currentStage) + "局「" + sm.GetStageName(sm.currentStage) + "」を前に", 20, Palette.Gold, "c3-mincho");
            stage.style.marginBottom = 4;
            scroll.Add(stage);
        }
        string titleText = pickCount > 1
            ? "仲間か強化を選んでください（" + pickCount + "枚のうち" + pickIndex + "枚目）"
            : "仲間か強化をひとつ選んでください";
        var title = UIFactory.Label(titleText, 34, Palette.Text, "c3-mincho");
        title.style.letterSpacing = 4;
        title.style.marginBottom = 14;
        scroll.Add(title);

        // カード（絵と名前だけ）
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.justifyContent = Justify.Center;
        row.style.marginBottom = 14;
        foreach (var option in choices)
            row.Add(option.IsTrain ? CreateTrainCard(option) : option.IsUpgrade ? CreateUpgradeCard(option) : CreatePieceCard(option));
        scroll.Add(row);

        // 画面下部: 選んだカードの説明
        detailPanel = UIFactory.Panel();
        detailPanel.style.width = Length.Percent(92);
        detailPanel.style.maxWidth = 1040;
        detailPanel.style.minHeight = 120;
        detailPanel.style.paddingTop = 16;
        detailPanel.style.paddingBottom = 16;
        detailPanel.style.paddingLeft = 28;
        detailPanel.style.paddingRight = 28;
        scroll.Add(detailPanel);
        ShowHint();

        // 決定と引き直しは横に並べる
        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.style.alignItems = Align.Center;
        buttons.style.marginTop = 14;
        confirmButton = UIFactory.Button("これに決める", OnConfirm, "c3-button--primary", "c3-button--big");
        confirmButton.SetEnabled(false);
        buttons.Add(confirmButton);
        if (onReroll != null && rerollsLeft > 0)
        {
            buttons.Add(UIFactory.Button("引き直す（残り" + rerollsLeft + "回）", () =>
            {
                onPieceSelected = null;
                onReroll();
            }));
        }
        scroll.Add(buttons);
    }

    // ------------------------------------------------------------
    // カード
    // ------------------------------------------------------------

    private VisualElement CreatePieceCard(DraftOption option)
    {
        PieceData piece = option.piece;
        var card = NewCard();

        card.Add(UIFactory.PieceIcon(piece, false, piece.portrait != null ? 130 : 104));

        var name = UIFactory.Label(piece.pieceName, 28, Palette.Text, "c3-mincho");
        name.style.marginTop = 10;
        card.Add(name);

        var chip = UIFactory.Chip(Palette.RarityName(piece.rarity), Palette.RarityLabel(piece.rarity));
        chip.style.marginTop = 6;
        card.Add(chip);

        card.RegisterCallback<ClickEvent>(evt => SelectCard(option));
        cards.Add(card);
        return card;
    }

    /// <summary>全軍強化のカード</summary>
    private VisualElement CreateUpgradeCard(DraftOption option)
    {
        var card = NewCard();

        var icon = new VisualElement();
        icon.style.width = 100;
        icon.style.height = 100;
        icon.style.marginTop = 4;
        icon.style.marginBottom = 4;
        icon.style.alignItems = Align.Center;
        icon.style.justifyContent = Justify.Center;
        icon.style.borderTopLeftRadius = icon.style.borderTopRightRadius = icon.style.borderBottomLeftRadius = icon.style.borderBottomRightRadius = 50;
        Color c = option.GlyphColor;
        icon.style.backgroundColor = new Color(c.r, c.g, c.b, 0.22f);
        icon.style.borderTopWidth = icon.style.borderBottomWidth = icon.style.borderLeftWidth = icon.style.borderRightWidth = 3;
        icon.style.borderTopColor = icon.style.borderBottomColor = icon.style.borderLeftColor = icon.style.borderRightColor = c;
        icon.pickingMode = PickingMode.Ignore;
        icon.Add(UIFactory.Label(option.Glyph, option.Glyph.Length > 1 ? 40 : 54, Color.Lerp(c, Color.white, 0.4f), "c3-mincho"));
        card.Add(icon);

        var name = UIFactory.Label(option.Title, 28, Palette.Text, "c3-mincho");
        name.style.marginTop = 10;
        card.Add(name);

        var chip = UpgradeChip(option);
        chip.style.marginTop = 6;
        card.Add(chip);
        if (option.fleetReward)
        {
            // S勝利のごほうびは海の色の縁で目立たせる
            card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = CutInUI.SeaLight;
            card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 3;
        }

        card.RegisterCallback<ClickEvent>(evt => SelectCard(option));
        cards.Add(card);
        return card;
    }

    /// <summary>鍛える札（部員の駒と、練度がどこまで上がるか）</summary>
    private VisualElement CreateTrainCard(DraftOption option)
    {
        PieceData member = option.train;
        var card = NewCard();
        card.Add(UIFactory.PieceIcon(member, false, 104));

        var name = UIFactory.Label(option.Title, 26, Palette.Text, "c3-mincho");
        name.style.marginTop = 10;
        card.Add(name);

        var chip = UIFactory.Chip("練度 +" + BalanceTuning.TrainXp, Palette.GoldLight);
        chip.style.marginTop = 6;
        card.Add(chip);

        RunMember m = GameManager.Instance != null ? GameManager.Instance.Roster.Get(member.pieceType) : null;
        if (m != null)
        {
            int after = RunRoster.StarsFor(m.rarity, m.xp + BalanceTuning.TrainXp);
            var stars = UIFactory.Label(StarText(m.stars) + (after > m.stars ? "  →  " + StarText(after) : ""), 18, Palette.GoldLight, "c3-bold");
            stars.style.marginTop = 6;
            card.Add(stars);
        }

        card.RegisterCallback<ClickEvent>(evt => SelectCard(option));
        cards.Add(card);
        return card;
    }

    /// <summary>★の並び（★★☆）</summary>
    public static string StarText(int stars)
    {
        string s = "";
        for (int i = 0; i < 3; i++) s += i < stars ? "★" : "☆";
        return s;
    }

    private static VisualElement NewCard()
    {
        var card = new VisualElement();
        card.AddToClassList("c3-card");
        return card;
    }

    private void SelectCard(DraftOption option)
    {
        selectedOption = option;
        showingPromoted = false;

        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayClick();

        for (int i = 0; i < cards.Count; i++)
            cards[i].EnableInClassList("c3-card--selected", i < currentChoices.Count && currentChoices[i] == option);

        if (option.IsTrain) RenderTrainDetail(option);
        else if (option.IsUpgrade) RenderUpgradeDetail(option);
        else RenderPieceDetail(option.piece, false);
        confirmButton.SetEnabled(true);
    }

    // ------------------------------------------------------------
    // 画面下部の説明
    // ------------------------------------------------------------

    private void ShowHint()
    {
        detailPanel.Clear();
        detailPanel.style.flexDirection = FlexDirection.Column;
        detailPanel.style.justifyContent = Justify.Center;
        var hint = UIFactory.Label("カードを選ぶと、ここに説明が表示されます", 20, Palette.TextSub);
        hint.style.unityTextAlign = TextAnchor.MiddleCenter;
        detailPanel.Add(hint);
    }

    private void RenderUpgradeDetail(DraftOption option)
    {
        detailPanel.Clear();
        detailPanel.style.flexDirection = FlexDirection.Column;
        detailPanel.style.justifyContent = Justify.FlexStart;

        var head = new VisualElement();
        head.style.flexDirection = FlexDirection.Row;
        head.style.alignItems = Align.Center;
        head.style.marginBottom = 12;
        head.Add(UIFactory.Label(option.Title, 30, Palette.Text, "c3-mincho"));
        var chip = UpgradeChip(option);
        chip.style.marginLeft = 12;
        head.Add(chip);
        detailPanel.Add(head);

        detailPanel.Add(UIFactory.Paragraph(option.Description, 21, Palette.Text));
        if (option.fleetReward)
        {
            var note = UIFactory.Paragraph("提督の艦隊がS勝利（艦娘が1隻も沈まずに作戦完了）したごほうびで、選択肢が1枚増えています。", 19, CutInUI.SeaLight);
            note.style.marginTop = 8;
            detailPanel.Add(note);
        }
    }

    private void RenderTrainDetail(DraftOption option)
    {
        detailPanel.Clear();
        detailPanel.style.flexDirection = FlexDirection.Column;
        detailPanel.style.justifyContent = Justify.FlexStart;

        var head = new VisualElement();
        head.style.flexDirection = FlexDirection.Row;
        head.style.alignItems = Align.Center;
        head.style.marginBottom = 12;
        head.Add(UIFactory.Label(option.Title, 30, Palette.Text, "c3-mincho"));
        var chip = UIFactory.Chip("部員を鍛える", Palette.GoldLight);
        chip.style.marginLeft = 12;
        head.Add(chip);
        detailPanel.Add(head);

        RunMember m = GameManager.Instance != null ? GameManager.Instance.Roster.Get(option.train.pieceType) : null;
        if (m != null)
        {
            int afterXp = m.xp + BalanceTuning.TrainXp;
            int afterStars = RunRoster.StarsFor(m.rarity, afterXp);
            string now = "いま: " + StarText(m.stars) + "  練度 " + RunRoster.XpText(m);
            string then = "鍛えると: " + StarText(afterStars) + "  練度 " + afterXp
                + (afterStars > m.stars ? "（★" + afterStars + "：" + RunRoster.StarEffectText(afterStars) + "）" : "");
            var progress = UIFactory.Paragraph(now + "\n" + then, 21, Palette.GoldLight);
            progress.style.marginBottom = 8;
            detailPanel.Add(progress);
        }
        detailPanel.Add(UIFactory.Paragraph(option.Description, 19, Palette.Text));
    }

    /// <summary>強化カードの種類の札（S勝利で増えた1枚は「S勝利のごほうび」）</summary>
    private static VisualElement UpgradeChip(DraftOption option)
    {
        return option.fleetReward ? UIFactory.Chip("S勝利のごほうび", CutInUI.SeaLight) : UIFactory.Chip("全軍強化", Palette.GoldLight);
    }

    private void RenderPieceDetail(PieceData piece, bool promoted)
    {
        detailPanel.Clear();
        detailPanel.style.flexDirection = FlexDirection.Row;
        detailPanel.style.justifyContent = Justify.FlexStart;
        promoted = promoted && piece.canPromote;

        string fullName = promoted ? piece.promotedName : piece.pieceName;
        string desc = promoted ? piece.promotedDescription : piece.description;
        int atk = (promoted ? piece.promotedATK : piece.baseATK) + PiecePool.RecruitBonusATK(piece);
        int def = promoted ? piece.promotedDEF : piece.baseDEF;
        int hp = (promoted ? piece.promotedHP : piece.baseHP) + PiecePool.RecruitBonusHP(piece);
        Rarity rarity = (promoted && piece.hasPromotedRarity) ? piece.promotedRarity : piece.rarity;

        // 左: 名前・ステータス・説明
        var left = new VisualElement();
        left.style.flexGrow = 1;
        left.style.flexShrink = 1;
        left.style.flexBasis = 0;

        var head = new VisualElement();
        head.style.flexDirection = FlexDirection.Row;
        head.style.alignItems = Align.Center;
        head.style.flexWrap = Wrap.Wrap;
        head.style.marginBottom = 12;
        head.Add(UIFactory.Label(fullName, 30, Palette.Text, "c3-mincho"));
        var rarityChip = UIFactory.Chip(Palette.RarityName(rarity), Palette.RarityLabel(rarity));
        rarityChip.style.marginLeft = 12;
        head.Add(rarityChip);
        if (promoted) head.Add(UIFactory.Chip("成り", Palette.EnemyLight));
        left.Add(head);

        left.Add(UIFactory.StatRow(atk, def, UIFactory.FormatHP(hp)));

        var descLabel = UIFactory.Paragraph(desc, 20, Palette.Text);
        descLabel.style.marginTop = 14;
        left.Add(descLabel);

        if (piece.canPromote)
        {
            bool nextState = !promoted;
            var toggle = UIFactory.Button(promoted ? "成る前を見る" : "成った姿を見る", () =>
            {
                showingPromoted = nextState;
                RenderPieceDetail(piece, nextState);
            });
            toggle.style.alignSelf = Align.FlexStart;
            toggle.style.marginTop = 16;
            toggle.style.marginLeft = 0;
            left.Add(toggle);
        }
        detailPanel.Add(left);

        // 区切り線
        var divider = new VisualElement();
        divider.style.width = 1;
        divider.style.marginLeft = 32;
        divider.style.marginRight = 32;
        divider.style.backgroundColor = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.25f);
        detailPanel.Add(divider);

        // 右: 動き
        var right = new VisualElement();
        right.style.alignItems = Align.Center;
        right.style.flexShrink = 0;
        right.Add(UIFactory.Label(promoted ? "動き（成り）" : "動き", 20, Palette.Gold, "c3-bold"));
        MoveDirection[] moves = UIFactory.MovesOf(piece, promoted);
        var grid = UIFactory.MoveGrid(moves, false, 22f);
        grid.style.marginTop = 10;
        right.Add(grid);
        Label immovable = UIFactory.ImmovableNote(moves);
        if (immovable != null) right.Add(immovable);
        else right.Add(UIFactory.MoveLegend());
        detailPanel.Add(right);
    }

    private void OnConfirm()
    {
        if (selectedOption == null) return;
        Hide();
        var callback = onPieceSelected;
        onPieceSelected = null;
        if (callback != null) callback(selectedOption);
    }

    public void Hide()
    {
        if (root == null) return;
        root.style.display = DisplayStyle.None;
        root.pickingMode = PickingMode.Ignore;
        root.Clear();
    }
}
