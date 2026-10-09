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
        return piece.currentHP + "|" + piece.ATK + "|" + piece.DEF + "|" + piece.MaxHP + "|" + (piece.isPromoted ? "1" : "0");
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
        var nameLabel = UIFactory.Label(fullName, 24, Palette.Text, "c3-mincho");
        nameLabel.style.whiteSpace = WhiteSpace.Normal;
        titleCol.Add(nameLabel);

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

        // 説明文
        var descLabel = UIFactory.Label(KinsokuHelper.Apply(desc), 15, Palette.TextSub);
        descLabel.style.whiteSpace = WhiteSpace.Normal;
        descLabel.style.marginTop = 12;
        content.Add(descLabel);

        content.Add(UIFactory.Separator());

        // 動き（敵駒は盤上の向きに合わせて上下反転）
        var moveTitle = UIFactory.Label(promoted ? "動き（成り）" : "動き", 15, Palette.Gold, "c3-bold");
        moveTitle.style.marginBottom = 6;
        content.Add(moveTitle);
        content.Add(UIFactory.MoveGrid(UIFactory.MovesOf(d, promoted), piece.team == Team.Enemy, 22f));

        if (d.canPromote)
        {
            bool nextState = !promoted;
            var toggle = UIFactory.Button(promoted ? "成る前を見る" : "成りを見る", () =>
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
        var hint = UIFactory.Label("駒をクリックすると、ここに詳しい情報が表示されます。自分の駒を選ぶと動ける場所が盤に表示されます。", 15, Palette.TextSub);
        hint.style.whiteSpace = WhiteSpace.Normal;
        hint.style.marginBottom = 12;
        content.Add(hint);

        content.Add(GuideRow(Badge(Palette.ATK), "攻撃力 … 相手に与えるダメージ"));
        content.Add(GuideRow(Badge(Palette.DEF), "防御力 … 受けるダメージを減らす"));
        content.Add(GuideRow(Badge(Palette.HP), "体力 … 0になると撃破される"));
        content.Add(UIFactory.Separator());
        content.Add(GuideRow(Badge(new Color(0.3f, 0.75f, 0.5f)), "移動できるマス"));
        content.Add(GuideRow(Badge(new Color(0.95f, 0.5f, 0.2f)), "移動できるが、敵に狙われるマス"));
        content.Add(GuideRow(Badge(Palette.AttackRing), "攻撃できる敵（予想ダメージ付き）"));
        content.Add(UIFactory.Separator());

        var rule = UIFactory.Label("ダメージ ＝ 攻撃力 − 防御力。倒しきれないときは攻撃した駒はその場に留まります。相手の「C3」を倒せば勝ち、自分の「C3」が倒されると負けです。", 14, Palette.TextSub);
        rule.style.whiteSpace = WhiteSpace.Normal;
        content.Add(rule);
    }

    private static VisualElement GuideRow(VisualElement icon, string text)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 6;
        row.Add(icon);
        var label = UIFactory.Label(text, 14, Palette.Text);
        label.style.marginLeft = 10;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.flexShrink = 1;
        row.Add(label);
        return row;
    }

    private static VisualElement Badge(Color color)
    {
        var dot = new VisualElement();
        dot.style.width = 14;
        dot.style.height = 14;
        dot.style.flexShrink = 0;
        dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius = dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 7;
        dot.style.backgroundColor = new Color(color.r, color.g, color.b, 1f);
        return dot;
    }
}
