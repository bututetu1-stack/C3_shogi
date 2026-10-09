using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class PieceSelectionUI : MonoBehaviour
{
    private UIDocument uiDocument;
    private VisualElement root;
    private List<PieceData> currentChoices;
    private System.Action<PieceData> onPieceSelected;
    private PieceData selectedPiece;
    private bool showingPromoted;
    private List<VisualElement> pieceIcons = new List<VisualElement>();
    private VisualElement detailPanel;
    private Button confirmButton;
    private static UnityEngine.TextCore.Text.FontAsset sdfFont;

    void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument != null)
        {
            root = uiDocument.rootVisualElement;
            root.Clear(); // UXML由来の子要素を除去
            root.style.display = DisplayStyle.None;
        }
    }

    private void EnsureRoot()
    {
        if (root != null) return;
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument != null)
            root = uiDocument.rootVisualElement;
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

    public void ShowSelection(List<PieceData> choices, System.Action<PieceData> callback)
    {
        EnsureRoot();
        if (root == null) return;

        currentChoices = choices;
        onPieceSelected = callback;
        selectedPiece = null;
        showingPromoted = false;
        pieceIcons.Clear();

        root.Clear();
        root.style.display = DisplayStyle.Flex;
        root.style.position = Position.Absolute;
        root.style.top = 0;
        root.style.left = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.FlexStart;
        root.style.backgroundColor = new Color(0, 0, 0, 0.85f);
        root.style.paddingTop = 40;
        ApplyFont(root);

        // タイトル
        var title = new Label("駒を選択");
        title.style.fontSize = 36;
        title.style.color = Color.white;
        title.style.marginBottom = 24;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        ApplyFont(title);
        root.Add(title);

        // 上部: 駒アイコン3つ横並び
        var iconRow = new VisualElement();
        iconRow.style.flexDirection = FlexDirection.Row;
        iconRow.style.justifyContent = Justify.Center;
        iconRow.style.alignItems = Align.Center;
        iconRow.style.marginBottom = 20;

        foreach (var piece in choices)
        {
            var iconWrapper = CreatePieceIcon(piece);
            iconRow.Add(iconWrapper);
        }
        root.Add(iconRow);

        // 下部: 詳細パネル（初期非表示）
        detailPanel = new VisualElement();
        detailPanel.style.width = 900;
        detailPanel.style.maxHeight = 600;
        detailPanel.style.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.98f);
        detailPanel.style.borderTopLeftRadius = 14;
        detailPanel.style.borderTopRightRadius = 14;
        detailPanel.style.borderBottomLeftRadius = 14;
        detailPanel.style.borderBottomRightRadius = 14;
        detailPanel.style.display = DisplayStyle.None;
        detailPanel.style.flexDirection = FlexDirection.Row;
        detailPanel.style.paddingTop = 20;
        detailPanel.style.paddingBottom = 20;
        detailPanel.style.paddingLeft = 20;
        detailPanel.style.paddingRight = 20;
        root.Add(detailPanel);

        // 決定ボタン（初期非表示）
        confirmButton = new Button();
        confirmButton.text = "決定";
        confirmButton.style.marginTop = 16;
        confirmButton.style.fontSize = 28;
        confirmButton.style.paddingTop = 12;
        confirmButton.style.paddingBottom = 12;
        confirmButton.style.paddingLeft = 60;
        confirmButton.style.paddingRight = 60;
        confirmButton.style.backgroundColor = new Color(0.3f, 0.5f, 0.9f);
        confirmButton.style.color = Color.white;
        confirmButton.style.borderTopLeftRadius = 10;
        confirmButton.style.borderTopRightRadius = 10;
        confirmButton.style.borderBottomLeftRadius = 10;
        confirmButton.style.borderBottomRightRadius = 10;
        confirmButton.style.display = DisplayStyle.None;
        confirmButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        ApplyFont(confirmButton);
        confirmButton.clicked += OnConfirm;
        root.Add(confirmButton);
    }

    private VisualElement CreatePieceIcon(PieceData piece)
    {
        var wrapper = new VisualElement();
        wrapper.style.alignItems = Align.Center;
        wrapper.style.marginLeft = 20;
        wrapper.style.marginRight = 20;

        // 五角形風の駒アイコン
        var icon = new VisualElement();
        icon.style.width = 120;
        icon.style.height = 120;
        icon.style.backgroundColor = ShogiPieceShape.GetRarityBodyColor(piece.rarity);
        icon.style.borderTopLeftRadius = 12;
        icon.style.borderTopRightRadius = 12;
        icon.style.borderBottomLeftRadius = 12;
        icon.style.borderBottomRightRadius = 12;
        icon.style.alignItems = Align.Center;
        icon.style.justifyContent = Justify.Center;
        icon.style.borderTopWidth = 3;
        icon.style.borderBottomWidth = 3;
        icon.style.borderLeftWidth = 3;
        icon.style.borderRightWidth = 3;
        icon.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        icon.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        icon.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        icon.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        // 縦書き2文字
        string dn = piece.displayName;
        string topChar = dn.Length > 0 ? dn.Substring(0, 1) : "";
        string bottomChar = dn.Length > 1 ? dn.Substring(1, 1) : "";

        var topLabel = new Label(topChar);
        topLabel.style.fontSize = 36;
        topLabel.style.color = new Color(0.15f, 0.15f, 0.15f);
        topLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        topLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        topLabel.style.marginBottom = -6;
        ApplyFont(topLabel);
        icon.Add(topLabel);

        var bottomLabel = new Label(bottomChar);
        bottomLabel.style.fontSize = 36;
        bottomLabel.style.color = new Color(0.15f, 0.15f, 0.15f);
        bottomLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        bottomLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        bottomLabel.style.marginTop = -6;
        ApplyFont(bottomLabel);
        icon.Add(bottomLabel);

        // クリックイベント
        PieceData capturedPiece = piece;
        icon.RegisterCallback<ClickEvent>(evt =>
        {
            SelectIcon(capturedPiece);
        });

        wrapper.Add(icon);
        pieceIcons.Add(icon);

        // 駒名ラベル
        var nameLabel = new Label(piece.pieceName);
        nameLabel.style.fontSize = 22;
        nameLabel.style.color = Color.white;
        nameLabel.style.marginTop = 8;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        ApplyFont(nameLabel);
        wrapper.Add(nameLabel);

        // レアリティ
        var rarityLabel = new Label(piece.rarity.ToString());
        rarityLabel.style.fontSize = 16;
        rarityLabel.style.color = GetRarityColor(piece.rarity);
        rarityLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        ApplyFont(rarityLabel);
        wrapper.Add(rarityLabel);

        return wrapper;
    }

    private void SelectIcon(PieceData piece)
    {
        selectedPiece = piece;
        showingPromoted = false;

        // ドラフト選択SE
        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayMoveEffect();

        // ハイライト更新
        for (int i = 0; i < pieceIcons.Count; i++)
        {
            Color borderColor;
            if (i < currentChoices.Count && currentChoices[i] == piece)
                borderColor = new Color(1f, 0.85f, 0.2f);
            else
                borderColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

            pieceIcons[i].style.borderTopColor = borderColor;
            pieceIcons[i].style.borderBottomColor = borderColor;
            pieceIcons[i].style.borderLeftColor = borderColor;
            pieceIcons[i].style.borderRightColor = borderColor;
        }

        RenderDetailPanel(piece, false);
        detailPanel.style.display = DisplayStyle.Flex;
        confirmButton.style.display = DisplayStyle.Flex;
    }

    private void RenderDetailPanel(PieceData piece, bool promoted)
    {
        detailPanel.Clear();

        bool usePromoted = promoted && piece.canPromote;
        string displayName = usePromoted ? piece.promotedDisplayName : piece.displayName;
        string fullName = usePromoted ? piece.promotedName : piece.pieceName;
        string desc = usePromoted ? piece.promotedDescription : piece.description;
        int atk = usePromoted ? piece.promotedATK : piece.baseATK;
        int def = usePromoted ? piece.promotedDEF : piece.baseDEF;
        int hp = usePromoted ? piece.promotedHP : piece.baseHP;

        // 左パネル: アイコン + ステータス
        var leftPanel = new VisualElement();
        leftPanel.style.width = 180;
        leftPanel.style.alignItems = Align.Center;
        leftPanel.style.paddingRight = 16;

        var icon = new VisualElement();
        icon.style.width = 100;
        icon.style.height = 100;
        icon.style.backgroundColor = ShogiPieceShape.GetRarityBodyColor(piece.rarity);
        icon.style.borderTopLeftRadius = 10;
        icon.style.borderTopRightRadius = 10;
        icon.style.borderBottomLeftRadius = 10;
        icon.style.borderBottomRightRadius = 10;
        icon.style.alignItems = Align.Center;
        icon.style.justifyContent = Justify.Center;
        icon.style.marginBottom = 10;

        var iconLabel = new Label(displayName);
        iconLabel.style.fontSize = 36;
        iconLabel.style.color = usePromoted ? new Color(0.85f, 0.1f, 0.1f) : new Color(0.15f, 0.15f, 0.15f);
        iconLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        iconLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        ApplyFont(iconLabel);
        icon.Add(iconLabel);
        leftPanel.Add(icon);

        var nameLabel = new Label(fullName);
        nameLabel.style.fontSize = 26;
        nameLabel.style.color = Color.white;
        nameLabel.style.marginBottom = 4;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        ApplyFont(nameLabel);
        leftPanel.Add(nameLabel);

        var rarityLabel = new Label(piece.rarity.ToString());
        rarityLabel.style.fontSize = 18;
        rarityLabel.style.color = GetRarityColor(piece.rarity);
        rarityLabel.style.marginBottom = 12;
        rarityLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        ApplyFont(rarityLabel);
        leftPanel.Add(rarityLabel);

        AddStatRow(leftPanel, "ATK", atk.ToString(), new Color(0.9f, 0.3f, 0.3f));
        AddStatRow(leftPanel, "DEF", def.ToString(), new Color(0.3f, 0.6f, 0.9f));
        string hpStr = hp >= 999 ? "∞" : hp.ToString();
        AddStatRow(leftPanel, "HP", hpStr, new Color(0.3f, 0.9f, 0.4f));
        detailPanel.Add(leftPanel);

        // 中央パネル: 移動範囲グリッド
        var centerPanel = new VisualElement();
        centerPanel.style.width = 340;
        centerPanel.style.alignItems = Align.Center;
        centerPanel.style.justifyContent = Justify.Center;
        centerPanel.style.paddingLeft = 10;
        centerPanel.style.paddingRight = 10;

        var moveTitle = new Label(usePromoted ? "移動範囲 (成り)" : "移動範囲");
        moveTitle.style.fontSize = 20;
        moveTitle.style.color = Color.white;
        moveTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        moveTitle.style.marginBottom = 8;
        moveTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        ApplyFont(moveTitle);
        centerPanel.Add(moveTitle);

        MoveDirection[] dirs = usePromoted && piece.promotedMoveDirections != null
            ? piece.promotedMoveDirections
            : piece.moveDirections;
        AddMoveGrid(centerPanel, dirs);
        detailPanel.Add(centerPanel);

        // 右パネル: 説明文 + 成りボタン
        var rightPanel = new VisualElement();
        rightPanel.style.flexGrow = 1;
        rightPanel.style.paddingLeft = 16;
        rightPanel.style.justifyContent = Justify.FlexStart;

        var descTitle = new Label("説明");
        descTitle.style.fontSize = 20;
        descTitle.style.color = new Color(0.7f, 0.7f, 0.7f);
        descTitle.style.marginBottom = 6;
        descTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        ApplyFont(descTitle);
        rightPanel.Add(descTitle);

        var descScroll = new ScrollView(ScrollViewMode.Vertical);
        descScroll.style.maxHeight = 260;
        descScroll.style.flexGrow = 1;

        var descLabel = new Label(KinsokuHelper.Apply(desc));
        descLabel.style.fontSize = 20;
        descLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
        descLabel.style.whiteSpace = WhiteSpace.Normal;
        ApplyFont(descLabel);
        descScroll.Add(descLabel);
        rightPanel.Add(descScroll);

        // 成りを見るボタン
        if (piece.canPromote)
        {
            var promoteBtn = new Button();
            promoteBtn.text = usePromoted ? "通常を見る" : "成りを見る";
            promoteBtn.style.marginTop = 10;
            promoteBtn.style.fontSize = 20;
            promoteBtn.style.paddingTop = 8;
            promoteBtn.style.paddingBottom = 8;
            promoteBtn.style.paddingLeft = 16;
            promoteBtn.style.paddingRight = 16;
            promoteBtn.style.backgroundColor = usePromoted ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.5f, 0.35f, 0.15f);
            promoteBtn.style.color = Color.white;
            promoteBtn.style.borderTopLeftRadius = 6;
            promoteBtn.style.borderTopRightRadius = 6;
            promoteBtn.style.borderBottomLeftRadius = 6;
            promoteBtn.style.borderBottomRightRadius = 6;
            promoteBtn.style.alignSelf = Align.FlexStart;
            ApplyFont(promoteBtn);

            PieceData capturedPiece = piece;
            bool nextState = !promoted;
            promoteBtn.clicked += () =>
            {
                showingPromoted = nextState;
                RenderDetailPanel(capturedPiece, nextState);
            };
            rightPanel.Add(promoteBtn);
        }

        detailPanel.Add(rightPanel);
    }

    private void AddStatRow(VisualElement parent, string label, string value, Color color)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.width = new Length(100, LengthUnit.Percent);
        row.style.marginBottom = 4;

        var lbl = new Label(label);
        lbl.style.fontSize = 20;
        lbl.style.color = color;
        lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
        ApplyFont(lbl);

        var val = new Label(value);
        val.style.fontSize = 20;
        val.style.color = Color.white;
        ApplyFont(val);

        row.Add(lbl);
        row.Add(val);
        parent.Add(row);
    }

    private void AddMoveGrid(VisualElement parent, MoveDirection[] dirs)
    {
        int gridSize = 9;
        int center = 4;

        var moveSet = new HashSet<Vector2Int>();
        if (dirs != null)
        {
            foreach (var dir in dirs)
            {
                int showDist = Mathf.Min(dir.maxDistance, 4);
                for (int d = 1; d <= showDist; d++)
                    moveSet.Add(dir.direction * d);
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
                cell.style.width = 32;
                cell.style.height = 32;
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

    private void OnConfirm()
    {
        if (selectedPiece == null) return;
        root.style.display = DisplayStyle.None;
        if (onPieceSelected != null)
            onPieceSelected(selectedPiece);
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
