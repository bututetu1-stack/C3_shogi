using UnityEngine;
using UnityEngine.UIElements;

public class PieceDetailUI : MonoBehaviour
{
    private UIDocument uiDocument;
    private VisualElement root;
    private VisualElement detailPanel;
    private PieceInstance lastShownPiece;
    private bool showingPromoted;
    private string lastStateKey;
    private bool lastPromoted;
    private static UnityEngine.TextCore.Text.FontAsset sdfFont;

    void Start()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument != null)
        {
            root = uiDocument.rootVisualElement;
            root.Clear(); // UXML由来の子要素を除去
            SetupRoot();
        }
    }

    void Update()
    {
        if (InputManager.Instance == null || detailPanel == null) return;
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
                HidePanel();
            }
            return;
        }

        // 表示中の駒のHP・ステータス・成り状態が変わったら描き直す
        if (viewed != null && GetStateKey(viewed) != lastStateKey)
        {
            // 成り・成り解除が起きたら表示もそれに合わせる
            if (viewed.isPromoted != lastPromoted)
                showingPromoted = viewed.isPromoted;
            RenderDetail(viewed, showingPromoted);
        }
    }

    private static string GetStateKey(PieceInstance piece)
    {
        return piece.currentHP + "|" + piece.ATK + "|" + piece.DEF + "|" + piece.MaxHP + "|" + (piece.isPromoted ? "1" : "0");
    }

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

    private void SetupRoot()
    {
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.style.width = Length.Percent(100);
        root.style.height = Length.Percent(100);
        root.pickingMode = PickingMode.Ignore;

        detailPanel = new VisualElement();
        detailPanel.style.position = Position.Absolute;
        detailPanel.style.top = 10;
        detailPanel.style.right = 10;
        detailPanel.style.bottom = 10;
        detailPanel.style.width = 440;
        detailPanel.style.maxHeight = Length.Percent(100);
        detailPanel.style.overflow = Overflow.Hidden;
        detailPanel.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        detailPanel.style.borderTopLeftRadius = 14;
        detailPanel.style.borderTopRightRadius = 14;
        detailPanel.style.borderBottomLeftRadius = 14;
        detailPanel.style.borderBottomRightRadius = 14;
        detailPanel.style.display = DisplayStyle.None;
        detailPanel.pickingMode = PickingMode.Position;

        root.Add(detailPanel);
    }

    private void RenderDetail(PieceInstance piece, bool promoted)
    {
        lastStateKey = GetStateKey(piece);
        lastPromoted = piece.isPromoted;

        detailPanel.Clear();
        detailPanel.style.display = DisplayStyle.Flex;

        var scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.style.flexGrow = 1;
        scrollView.style.paddingTop = 16;
        scrollView.style.paddingBottom = 16;
        scrollView.style.paddingLeft = 16;
        scrollView.style.paddingRight = 16;
        ApplyFont(scrollView);

        PieceData d = piece.data;
        promoted = promoted && d.canPromote;
        // 現在の姿を見ているなら実際の値（バフ込み）、もう一方の姿ならデータ上の値を表示
        bool isCurrentForm = (promoted == piece.isPromoted);
        string displayName = isCurrentForm ? piece.DisplayName : (promoted ? d.promotedDisplayName : d.displayName);
        string fullName = isCurrentForm ? piece.FullName : (promoted ? d.promotedName : d.pieceName);
        string desc = isCurrentForm ? piece.Description : (promoted ? d.promotedDescription : d.description);
        int atk = isCurrentForm ? piece.ATK : (promoted ? d.promotedATK : d.baseATK);
        int def = isCurrentForm ? piece.DEF : (promoted ? d.promotedDEF : d.baseDEF);
        int hp = isCurrentForm ? piece.MaxHP : (promoted ? d.promotedHP : d.baseHP);

        // レアリティ判定（成り表示時は成りレアリティを使用）
        Rarity displayRarity = (promoted && d.hasPromotedRarity) ? d.promotedRarity : d.rarity;

        // 駒アイコン
        var icon = new VisualElement();
        icon.style.width = 100;
        icon.style.height = 100;
        icon.style.alignSelf = Align.Center;
        icon.style.marginBottom = 14;
        icon.style.borderTopLeftRadius = 12;
        icon.style.borderTopRightRadius = 12;
        icon.style.borderBottomLeftRadius = 12;
        icon.style.borderBottomRightRadius = 12;
        icon.style.backgroundColor = ShogiPieceShape.GetRarityBodyColor(displayRarity);
        icon.style.alignItems = Align.Center;
        icon.style.justifyContent = Justify.Center;

        var iconLabel = new Label(displayName);
        iconLabel.style.fontSize = 38;
        iconLabel.style.color = new Color(0.15f, 0.15f, 0.15f);
        iconLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        iconLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        ApplyFont(iconLabel);
        icon.Add(iconLabel);
        scrollView.Add(icon);

        // 名前
        var nameLabel = new Label(fullName);
        nameLabel.style.fontSize = 32;
        nameLabel.style.color = Color.white;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        nameLabel.style.marginBottom = 4;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        ApplyFont(nameLabel);
        scrollView.Add(nameLabel);

        // チーム
        string teamText = piece.team == Team.Player ? "味方" : "敵";
        Color teamColor = piece.team == Team.Player ? new Color(0.4f, 0.7f, 1f) : new Color(1f, 0.4f, 0.4f);
        var teamLabel = new Label(teamText);
        teamLabel.style.fontSize = 20;
        teamLabel.style.color = teamColor;
        teamLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        teamLabel.style.marginBottom = 4;
        ApplyFont(teamLabel);
        scrollView.Add(teamLabel);

        // レアリティ
        var rarityLabel = new Label(GetRarityName(displayRarity));
        rarityLabel.style.fontSize = 20;
        rarityLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        rarityLabel.style.marginBottom = 14;
        rarityLabel.style.color = GetRarityColor(displayRarity);
        ApplyFont(rarityLabel);
        scrollView.Add(rarityLabel);

        // 説明文
        var descLabel = new Label(KinsokuHelper.Apply(desc));
        descLabel.style.fontSize = 22;
        descLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
        descLabel.style.whiteSpace = WhiteSpace.Normal;
        descLabel.style.marginBottom = 18;
        ApplyFont(descLabel);
        scrollView.Add(descLabel);

        // ステータス
        AddStatRow(scrollView, "ATK", atk.ToString(), new Color(0.9f, 0.3f, 0.3f));
        AddStatRow(scrollView, "DEF", def.ToString(), new Color(0.3f, 0.6f, 0.9f));
        string hpMax = hp >= 999 ? "∞" : hp.ToString();
        string hpCur = piece.currentHP >= 999 ? "∞" : piece.currentHP.ToString();
        string hpDisplay = isCurrentForm ? hpCur + " / " + hpMax : hpMax;
        AddStatRow(scrollView, "HP", hpDisplay, new Color(0.3f, 0.9f, 0.4f));

        AddSeparator(scrollView);

        // 移動範囲
        var moveTitle = new Label(promoted ? "移動範囲 (成り)" : "移動範囲");
        moveTitle.style.fontSize = 22;
        moveTitle.style.color = Color.white;
        moveTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        moveTitle.style.marginBottom = 10;
        moveTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        ApplyFont(moveTitle);
        scrollView.Add(moveTitle);

        MoveDirection[] dirs = promoted && d.promotedMoveDirections != null && d.promotedMoveDirections.Length > 0
            ? d.promotedMoveDirections
            : d.moveDirections;
        // 敵駒は盤上の向きに合わせて上下反転して表示
        AddMoveGrid(scrollView, dirs, piece.team == Team.Enemy);

        // 成りを見るボタン
        if (piece.data.canPromote)
        {
            AddSeparator(scrollView);

            var promoteBtn = new Button();
            promoteBtn.text = promoted ? "通常を見る" : "成りを見る";
            promoteBtn.style.marginTop = 8;
            promoteBtn.style.fontSize = 22;
            promoteBtn.style.paddingTop = 10;
            promoteBtn.style.paddingBottom = 10;
            promoteBtn.style.paddingLeft = 24;
            promoteBtn.style.paddingRight = 24;
            promoteBtn.style.backgroundColor = promoted ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.8f, 0.6f, 0.2f);
            promoteBtn.style.color = Color.white;
            promoteBtn.style.borderTopLeftRadius = 8;
            promoteBtn.style.borderTopRightRadius = 8;
            promoteBtn.style.borderBottomLeftRadius = 8;
            promoteBtn.style.borderBottomRightRadius = 8;
            promoteBtn.style.alignSelf = Align.Center;
            ApplyFont(promoteBtn);

            bool nextState = !promoted;
            PieceInstance capturedPiece = piece;
            promoteBtn.clicked += () => {
                showingPromoted = nextState;
                RenderDetail(capturedPiece, nextState);
            };
            scrollView.Add(promoteBtn);
        }

        detailPanel.Add(scrollView);
    }

    private void AddStatRow(VisualElement parent, string label, string value, Color color)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.marginBottom = 8;

        var lbl = new Label(label);
        lbl.style.fontSize = 22;
        lbl.style.color = color;
        lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
        ApplyFont(lbl);

        var val = new Label(value);
        val.style.fontSize = 22;
        val.style.color = Color.white;
        ApplyFont(val);

        row.Add(lbl);
        row.Add(val);
        parent.Add(row);
    }

    private void AddSeparator(VisualElement parent)
    {
        var sep = new VisualElement();
        sep.style.height = 1;
        sep.style.backgroundColor = new Color(0.35f, 0.35f, 0.4f);
        sep.style.marginTop = 10;
        sep.style.marginBottom = 10;
        parent.Add(sep);
    }

    private void AddMoveGrid(VisualElement parent, MoveDirection[] dirs, bool flipY)
    {
        int gridSize = 9;
        int center = 4;

        var moveSet = new System.Collections.Generic.HashSet<Vector2Int>();
        if (dirs != null)
        {
            foreach (var dir in dirs)
            {
                Vector2Int step = flipY ? new Vector2Int(dir.direction.x, -dir.direction.y) : dir.direction;
                int showDist = Mathf.Min(dir.maxDistance, 4);
                for (int d = 1; d <= showDist; d++)
                    moveSet.Add(step * d);
            }
        }

        var grid = new VisualElement();
        grid.style.alignSelf = Align.Center;

        for (int y = gridSize - 1; y >= 0; y--)
        {
            var rowElement = new VisualElement();
            rowElement.style.flexDirection = FlexDirection.Row;

            for (int x = 0; x < gridSize; x++)
            {
                var cell = new VisualElement();
                cell.style.width = 36;
                cell.style.height = 36;
                cell.style.marginLeft = 1;
                cell.style.marginRight = 1;
                cell.style.marginTop = 1;
                cell.style.marginBottom = 1;
                cell.style.borderTopLeftRadius = 4;
                cell.style.borderTopRightRadius = 4;
                cell.style.borderBottomLeftRadius = 4;
                cell.style.borderBottomRightRadius = 4;

                int dx = x - center;
                int dy = y - center;

                if (dx == 0 && dy == 0)
                    cell.style.backgroundColor = new Color(0.9f, 0.9f, 0.3f);
                else if (moveSet.Contains(new Vector2Int(dx, dy)))
                    cell.style.backgroundColor = new Color(0.4f, 0.85f, 0.4f, 0.8f);
                else
                    cell.style.backgroundColor = new Color(0.2f, 0.2f, 0.25f);

                rowElement.Add(cell);
            }
            grid.Add(rowElement);
        }
        parent.Add(grid);
    }

    private void HidePanel()
    {
        if (detailPanel != null)
            detailPanel.style.display = DisplayStyle.None;
    }

    private string GetRarityName(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Normal: return "Normal";
            case Rarity.Rare: return "Rare";
            case Rarity.SuperRare: return "Super Rare";
            case Rarity.Legend: return "Legend";
            case Rarity.Bronze: return "Bronze";
            default: return "";
        }
    }

    private Color GetRarityColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Bronze: return new Color(0.72f, 0.5f, 0.3f);
            case Rarity.Normal: return new Color(0.7f, 0.7f, 0.7f);
            case Rarity.Rare: return new Color(0.5f, 0.7f, 0.95f);
            case Rarity.SuperRare: return new Color(0.9f, 0.75f, 0.2f);
            case Rarity.Legend: return new Color(1f, 0.5f, 0.2f);
            default: return Color.white;
        }
    }
}
