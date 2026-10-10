using UnityEngine;
using UnityEngine.UIElements;

/// <summary>画面右の駒の詳細パネル（何も選んでいないときは操作ガイド）</summary>
public class PieceDetailUI : MonoBehaviour
{
    private VisualElement panel;
    private ScrollView content;
    private PieceInstance lastShownPiece;
    private bool showingPromoted;
    private string lastStateKey;
    private bool lastPromoted;
    private bool guideShown;

    void Start()
    {
        UIDocument uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;
        VisualElement root = UIFactory.SetupRoot(uiDocument);

        panel = UIFactory.Panel();
        panel.style.position = Position.Absolute;
        panel.style.right = 16;
        panel.style.top = CameraFitter.TopReserve;
        panel.style.bottom = CameraFitter.BottomReserve;
        panel.style.width = CameraFitter.RightReserve - 32;
        panel.style.paddingRight = 8;
        panel.pickingMode = PickingMode.Position;
        root.Add(panel);

        content = new ScrollView(ScrollViewMode.Vertical);
        content.style.flexGrow = 1;
        content.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        panel.Add(content);

        ShowGuide();
    }

    void Update()
    {
        if (InputManager.Instance == null || content == null) return;

        var viewed = InputManager.Instance.GetViewedPiece();
        if (viewed != lastShownPiece)
        {
            lastShownPiece = viewed;
            if (viewed != null)
            {
                // 成っている駒は成り後の姿を初期表示
                showingPromoted = viewed.isPromoted;
                RenderDetail(viewed, showingPromoted);
            }
            else
            {
                ShowGuide();
            }
            return;
        }

        // 表示中の駒のHP・ステータス・成り状態が変わったら描き直す
        if (viewed != null && GetStateKey(viewed) != lastStateKey)
        {
            if (viewed.isPromoted != lastPromoted)
                showingPromoted = viewed.isPromoted;
            RenderDetail(viewed, showingPromoted);
        }
    }

    private static string GetStateKey(PieceInstance piece)
    {
        RunMember m = piece.team == Team.Player && GameManager.Instance != null ? GameManager.Instance.Roster.Get(piece.data.pieceType) : null;
        return piece.currentHP + "|" + piece.ATK + "|" + piece.DEF + "|" + piece.MaxHP + "|" + (piece.isPromoted ? "1" : "0") + (piece.IsVeteran ? "v" : "")
            + (m != null ? "|" + m.xp : "") + "|" + piece.awakenCharge + (piece.awakened ? "a" : "") + (piece.isSealed ? "s" : "") + "|" + AbilitySystem.StatusLine(piece);
    }

    // ------------------------------------------------------------
    // 駒の詳細
    // ------------------------------------------------------------

    private void RenderDetail(PieceInstance piece, bool promoted)
    {
        guideShown = false;
        lastStateKey = GetStateKey(piece);
        lastPromoted = piece.isPromoted;
        content.Clear();

        PieceData d = piece.data;
        promoted = promoted && d.canPromote;
        // 現在の姿を見ているなら実際の値（バフ込み）、もう一方の姿ならデータ上の値を表示
        bool isCurrentForm = (promoted == piece.isPromoted);
        string fullName = isCurrentForm ? piece.FullName : (promoted ? d.promotedName : d.pieceName);
        string desc = isCurrentForm ? piece.Description : (promoted ? d.promotedDescription : d.description);
        int atk = isCurrentForm ? piece.ATK : (promoted ? d.promotedATK : d.baseATK);
        int def = isCurrentForm ? piece.DEF : (promoted ? d.promotedDEF : d.baseDEF);
        int hpMax = isCurrentForm ? piece.MaxHP : (promoted ? d.promotedHP : d.baseHP);
        Rarity rarity = (promoted && d.hasPromotedRarity) ? d.promotedRarity : d.rarity;

        // 見出し: アイコン + 名前・陣営・レアリティ
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 12;
        header.Add(UIFactory.PieceIcon(d, promoted, d.portrait != null ? 120 : 84));

        var titleCol = new VisualElement();
        titleCol.style.marginLeft = 12;
        titleCol.style.flexGrow = 1;
        titleCol.style.flexShrink = 1;
        titleCol.style.minWidth = 0;
        titleCol.Add(UIFactory.Paragraph(fullName, 27, Palette.Text, "c3-mincho"));

        var chips = new VisualElement();
        chips.style.flexDirection = FlexDirection.Row;
        chips.style.flexWrap = Wrap.Wrap;
        chips.style.alignItems = Align.FlexStart;
        chips.style.marginTop = 6;
        chips.style.marginLeft = -3;
        chips.Add(UIFactory.Chip(piece.team == Team.Player ? "味方" : "敵", Palette.TeamLight(piece.team)));
        chips.Add(UIFactory.Chip(Palette.RarityName(rarity), Palette.RarityLabel(rarity)));
        if (promoted) chips.Add(UIFactory.Chip("成り", Palette.EnemyLight));
        titleCol.Add(chips);
        header.Add(titleCol);
        content.Add(header);

        // ステータス
        string hpText = isCurrentForm
            ? UIFactory.FormatHP(piece.currentHP) + (piece.currentHP >= 999 ? "" : "/" + UIFactory.FormatHP(hpMax))
            : UIFactory.FormatHP(hpMax);
        content.Add(UIFactory.StatRow(atk, def, hpText));

        // 部員の練度（周のあいだ残る）
        RunMember member = piece.team == Team.Player && GameManager.Instance != null ? GameManager.Instance.Roster.Get(d.pieceType) : null;
        if (member != null)
        {
            var growth = UIFactory.Label("練度 " + PieceSelectionUI.StarText(member.stars) + "　" + RunRoster.XpText(member), 19, Palette.GoldLight, "c3-bold");
            growth.style.marginTop = 10;
            content.Add(growth);
            var effects = UIFactory.Paragraph("★1 " + RunRoster.StarEffectText(1) + "　★2 " + RunRoster.StarEffectText(2) + "　★3 " + RunRoster.StarEffectText(3)
                + "。敵を倒す・局を生き残る・能力が決まると練度がたまる。", 15, Palette.TextSub);
            content.Add(effects);
            if (d.canAwaken && member.stars >= 3)
            {
                string awake = piece.awakened ? "覚醒「" + d.awakenedName + "」"
                    : "覚醒まで: この局の活躍あと" + Mathf.Max(0, BalanceTuning.AwakenActivities - piece.awakenCharge) + "（倒す・能力が決まる）";
                var a = UIFactory.Label(awake, 18, d.awakenColor, "c3-bold");
                a.style.marginTop = 6;
                content.Add(a);
            }
        }
        string status = AbilitySystem.StatusLine(piece);
        if (status != null)
        {
            var statusLabel = UIFactory.Label(status, 18, Palette.GoldLight, "c3-bold");
            statusLabel.style.marginTop = 6;
            content.Add(statusLabel);
        }
        if (piece.isSealed)
        {
            var sealedNote = UIFactory.Label("封印されている（能力が止まっている）", 17, new Color(0.45f, 1f, 0.6f), "c3-bold");
            sealedNote.style.marginTop = 6;
            content.Add(sealedNote);
        }

        // 説明文
        var descLabel = UIFactory.Paragraph(desc, 18, Palette.Text);
        descLabel.style.marginTop = 14;
        content.Add(descLabel);

        // 説明と動きの図の間はしっかり空ける
        var sep = UIFactory.Separator();
        sep.style.marginTop = 18;
        sep.style.marginBottom = 16;
        content.Add(sep);

        // 動き（敵駒は盤上の向きに合わせて上下反転）
        var moveTitle = UIFactory.Label(promoted ? "動き（成り）" : "動き", 18, Palette.Gold, "c3-bold");
        moveTitle.style.marginBottom = 8;
        content.Add(moveTitle);
        MoveDirection[] moves = UIFactory.MovesOf(d, promoted);
        if (isCurrentForm && piece.IsVeteran && d.veteranMoveDirections != null && d.veteranMoveDirections.Length > 0)
            moves = d.veteranMoveDirections;
        content.Add(UIFactory.MoveGrid(moves, piece.team == Team.Enemy, 24f));
        Label immovable = UIFactory.ImmovableNote(moves);
        if (immovable != null) content.Add(immovable);
        else content.Add(UIFactory.MoveLegend());

        if (d.canPromote)
        {
            bool nextState = !promoted;
            var toggle = UIFactory.Button(promoted ? "成る前を見る" : "成った姿を見る", () =>
            {
                showingPromoted = nextState;
                RenderDetail(piece, nextState);
            });
            toggle.style.marginTop = 12;
            toggle.style.alignSelf = Align.Center;
            content.Add(toggle);
        }
    }

    // ------------------------------------------------------------
    // 操作ガイド
    // ------------------------------------------------------------

    private void ShowGuide()
    {
        if (guideShown || content == null) return;
        guideShown = true;
        lastStateKey = null;
        content.Clear();

        content.Add(UIFactory.PanelTitle("駒の情報"));
        var hint = UIFactory.Paragraph("駒をクリックすると、ここに詳しい情報が表示されます。自分の駒を選ぶと、動ける場所が盤に表示されます。", 18, Palette.TextSub);
        hint.style.marginBottom = 14;
        content.Add(hint);

        content.Add(GuideRow(Badge(Palette.ATK), "攻撃力 … 相手に与えるダメージ"));
        content.Add(GuideRow(Badge(Palette.DEF), "防御力 … 受けるダメージを減らす"));
        content.Add(GuideRow(Badge(Palette.HP), "体力 … 0になると撃破される"));
        content.Add(UIFactory.Separator());
        content.Add(GuideRow(Badge(new Color(0.3f, 0.75f, 0.5f)), "移動できるマス"));
        content.Add(GuideRow(Badge(new Color(0.95f, 0.5f, 0.2f)), "移動できるが、敵に狙われるマス"));
        content.Add(GuideRow(Badge(Palette.AttackRing), "攻撃できる敵（予想ダメージ付き）"));
        content.Add(UIFactory.Separator());

        content.Add(UIFactory.Paragraph("ダメージは「攻撃力 − 防御力」。倒しきれないときは、攻撃した駒はその場に留まります。", 17, Palette.TextSub));
        var rule = UIFactory.Paragraph("相手の「C3」を倒せば勝ち、自分の「C3」が倒されると負けです。", 17, Palette.TextSub);
        rule.style.marginTop = 8;
        content.Add(rule);
    }

    private static VisualElement GuideRow(VisualElement icon, string text)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 8;
        row.Add(icon);
        var label = UIFactory.Paragraph(text, 17, Palette.Text);
        label.style.marginLeft = 10;
        label.style.flexShrink = 1;
        label.style.flexGrow = 1;
        row.Add(label);
        return row;
    }

    private static VisualElement Badge(Color color)
    {
        var dot = new VisualElement();
        dot.style.width = 16;
        dot.style.height = 16;
        dot.style.flexShrink = 0;
        dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius = dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 8;
        dot.style.backgroundColor = new Color(color.r, color.g, color.b, 1f);
        return dot;
    }
}
