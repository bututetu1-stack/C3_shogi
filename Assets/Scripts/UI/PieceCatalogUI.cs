using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 駒一覧（図鑑）。タイトル画面と、対局中の上部のボタンから開く。
/// 分類（部員・提督の艦隊・敵・将棋の駒）ごとに駒を並べ、選ぶと右に数値・動き・説明を出す。
/// 駒の絵と動きの図は、敵の駒も正位置（駒から見て上が前）で表示する。
/// </summary>
public class PieceCatalogUI : MonoBehaviour
{
    private class Group
    {
        public string title;
        public PieceType[] types;
        public Group(string title, params PieceType[] types) { this.title = title; this.types = types; }
    }

    private static readonly Group[] Groups =
    {
        new Group("部員",
            PieceType.Nako, PieceType.Monotetsu, PieceType.Monin, PieceType.Boku, PieceType.Wotsu, PieceType.Kei,
            PieceType.Kipu, PieceType.Rihaku, PieceType.SN, PieceType.Konishiki, PieceType.Yuu,
            PieceType.Mitsuharu, PieceType.Niko, PieceType.Shimesaba, PieceType.Kawasemi, PieceType.Chuka, PieceType.Dopa),
        new Group("提督の艦隊",
            PieceType.KanmusuDD, PieceType.KanmusuCL, PieceType.KanmusuCA, PieceType.KanmusuBB, PieceType.KanmusuCV, PieceType.KanmusuSS,
            PieceType.Shinkai, PieceType.ShinkaiElite, PieceType.ShinkaiFlagship, PieceType.ShinkaiHime),
        new Group("敵",
            PieceType.Kishou, PieceType.Kagenin, PieceType.Teppeki, PieceType.Tengu, PieceType.Fujin, PieceType.Raitei,
            PieceType.Ryuujin, PieceType.Enmashi, PieceType.Gundaishou, PieceType.Yomigaeru, PieceType.Dokuro, PieceType.Maou),
        new Group("将棋の駒",
            PieceType.Pawn, PieceType.Lance, PieceType.Knight, PieceType.Silver, PieceType.Gold, PieceType.Bishop, PieceType.Rook, PieceType.C3)
    };

    private static PieceCatalogUI instance;
    private UIDocument uiDocument;
    private VisualElement overlay;
    private VisualElement tabRow;
    private VisualElement cardGrid;
    private VisualElement detail;
    private readonly List<Button> tabs = new List<Button>();
    private readonly List<VisualElement> cards = new List<VisualElement>();
    private int groupIndex;
    private PieceData selected;
    private bool showPromoted;
    private bool showVeteran;
    private bool showAwakened;

    public static bool IsOpen { get { return instance != null && instance.overlay != null; } }

    public static void Open()
    {
        Ensure().Show();
    }

    public static void Close()
    {
        if (instance != null) instance.Hide();
    }

    private static PieceCatalogUI Ensure()
    {
        if (instance != null) return instance;
        var obj = new GameObject("PieceCatalogUI");
        instance = obj.AddComponent<PieceCatalogUI>();
        instance.uiDocument = obj.AddComponent<UIDocument>();
        foreach (var d in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            if (d != instance.uiDocument && d.panelSettings != null)
            {
                instance.uiDocument.panelSettings = d.panelSettings;
                break;
            }
        }
        instance.uiDocument.sortingOrder = 95;   // タイトル画面（90）より上
        return instance;
    }

    private void Show()
    {
        VisualElement root = UIFactory.SetupRoot(uiDocument);
        root.Clear();

        overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = overlay.style.right = overlay.style.top = overlay.style.bottom = 0;
        overlay.style.backgroundColor = new Color(0.03f, 0.025f, 0.02f, 1f);   // 別ページなので後ろの画面は透かさない
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.pickingMode = PickingMode.Position;   // 盤へのクリックを遮る
        root.Add(overlay);

        var panel = UIFactory.Panel();
        panel.style.width = Length.Percent(94);
        panel.style.maxWidth = 1500;
        panel.style.height = Length.Percent(90);
        panel.style.paddingLeft = panel.style.paddingRight = 24;
        overlay.Add(panel);

        // 見出し・分類のタブ・閉じる
        var head = new VisualElement();
        head.style.flexDirection = FlexDirection.Row;
        head.style.alignItems = Align.Center;
        head.style.flexWrap = Wrap.Wrap;
        head.style.marginBottom = 10;
        var title = UIFactory.Label("駒一覧", 32, Palette.GoldLight, "c3-mincho");
        title.style.letterSpacing = 6;
        title.style.marginRight = 24;
        head.Add(title);
        tabRow = new VisualElement();
        tabRow.style.flexDirection = FlexDirection.Row;
        tabRow.style.flexWrap = Wrap.Wrap;
        tabRow.style.flexGrow = 1;
        tabs.Clear();
        for (int i = 0; i < Groups.Length; i++)
        {
            int index = i;
            var tab = UIFactory.Button(Groups[i].title, () => SelectGroup(index));
            tab.style.marginRight = 8;
            tabs.Add(tab);
            tabRow.Add(tab);
        }
        head.Add(tabRow);
        head.Add(UIFactory.Button("閉じる", Hide));
        panel.Add(head);

        // 左に駒の札、右に選んだ駒の説明
        var body = new VisualElement();
        body.style.flexDirection = FlexDirection.Row;
        body.style.flexGrow = 1;
        body.style.flexShrink = 1;
        body.style.minHeight = 0;
        panel.Add(body);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1;
        scroll.style.flexShrink = 1;
        scroll.style.flexBasis = 0;
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        cardGrid = scroll.contentContainer;
        cardGrid.style.flexDirection = FlexDirection.Row;
        cardGrid.style.flexWrap = Wrap.Wrap;
        cardGrid.style.alignContent = Align.FlexStart;
        body.Add(scroll);

        var detailScroll = new ScrollView(ScrollViewMode.Vertical);
        detailScroll.style.width = 560;
        detailScroll.style.flexShrink = 0;
        detailScroll.style.marginLeft = 18;
        detailScroll.style.paddingLeft = 18;
        detailScroll.style.borderLeftWidth = 1;
        detailScroll.style.borderLeftColor = Palette.PanelBorder;
        detail = detailScroll.contentContainer;
        body.Add(detailScroll);

        SelectGroup(groupIndex);
    }

    private void Hide()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            uiDocument.rootVisualElement.Clear();
            uiDocument.rootVisualElement.pickingMode = PickingMode.Ignore;
        }
        overlay = null;
    }

    private void SelectGroup(int index)
    {
        groupIndex = index;
        for (int i = 0; i < tabs.Count; i++)
        {
            tabs[i].EnableInClassList("c3-button--primary", i == index);
        }

        cardGrid.Clear();
        cards.Clear();
        PieceData first = null;
        BoardManager bm = BoardManager.Instance;
        foreach (PieceType type in Groups[index].types)
        {
            PieceData data = bm != null ? bm.GetPieceDataByType(type) : null;
            if (data == null) continue;
            if (first == null) first = data;
            VisualElement card = CreateCard(data);
            cards.Add(card);
            cardGrid.Add(card);
        }
        Select(first, false);
    }

    private VisualElement CreateCard(PieceData data)
    {
        var card = new VisualElement();
        card.AddToClassList("c3-card");
        card.style.width = 150;
        card.style.marginRight = 10;
        card.style.marginBottom = 10;
        card.style.paddingTop = 10;
        card.style.paddingBottom = 10;
        card.userData = data;

        // 駒の絵は敵の駒も正位置で
        card.Add(UIFactory.PieceIcon(data, false, 76f));
        var name = UIFactory.Label(CardName(data), 21, Palette.Text, "c3-mincho");
        name.style.marginTop = 6;
        card.Add(name);
        var chip = UIFactory.Chip(Palette.RarityName(data.rarity), Palette.RarityLabel(data.rarity));
        chip.style.marginTop = 4;
        card.Add(chip);

        card.RegisterCallback<ClickEvent>(evt => Select(data, false));
        return card;
    }

    /// <summary>札の名前（深海はランクで呼び分ける）</summary>
    private static string CardName(PieceData data)
    {
        switch (data.pieceType)
        {
            case PieceType.Shinkai: return "深海";
            case PieceType.ShinkaiElite: return "深海 elite";
            case PieceType.ShinkaiFlagship: return "深海 flagship";
            case PieceType.ShinkaiHime: return "深海棲姫";
            default: return data.displayName;
        }
    }

    private void Select(PieceData data, bool promoted, bool veteran = false)
    {
        selected = data;
        showPromoted = promoted && data != null && data.canPromote;
        showVeteran = veteran && !showPromoted && HasVeteran(data);
        showAwakened = false;
    }

    private void SelectAwakened(PieceData data)
    {
        Select(data, false);
        showAwakened = data != null && data.canAwaken;
        RenderDetail();
        foreach (var card in cards)
        {
            bool on = card.userData == (object)data;
            Color border = on ? Palette.GoldLight : Palette.PanelBorder;
            card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = border;
            card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = on ? 3 : 1;
        }
        RenderDetail();
    }

    /// <summary>作戦完了で生還したあとの姿（物鉄・改）があるか</summary>
    private static bool HasVeteran(PieceData d)
    {
        return d != null && d.veteranMoveDirections != null && d.veteranMoveDirections.Length > 0;
    }

    private void RenderDetail()
    {
        detail.Clear();
        PieceData d = selected;
        if (d == null) return;
        bool promoted = showPromoted;
        bool veteran = showVeteran;
        bool awakened = showAwakened;

        var top = new VisualElement();
        top.style.flexDirection = FlexDirection.Row;
        top.style.alignItems = Align.Center;
        top.Add(UIFactory.PieceIcon(d, promoted, 96f));
        var names = new VisualElement();
        names.style.marginLeft = 14;
        names.style.flexShrink = 1;
        names.style.flexGrow = 1;
        names.style.alignItems = Align.FlexStart;
        string displayName = awakened ? d.awakenedName : promoted ? d.promotedDisplayName : (veteran && !string.IsNullOrEmpty(d.veteranName) ? d.veteranName : CardName(d));
        string fullName = awakened ? d.pieceName + "の覚醒" : promoted ? d.promotedName : (veteran ? null : d.pieceName);
        names.Add(UIFactory.Label(displayName, 30, Palette.Text, "c3-mincho"));
        if (!string.IsNullOrEmpty(fullName) && fullName != displayName)
            names.Add(UIFactory.Label(fullName, 17, Palette.TextSub));
        var chips = new VisualElement();
        chips.style.flexDirection = FlexDirection.Row;
        chips.style.flexWrap = Wrap.Wrap;
        chips.style.alignItems = Align.Center;
        chips.style.marginTop = 4;
        Rarity rarity = promoted && d.hasPromotedRarity ? d.promotedRarity : d.rarity;
        chips.Add(UIFactory.Chip(Palette.RarityName(rarity), Palette.RarityLabel(rarity)));
        if (promoted || veteran || awakened)
        {
            var promo = UIFactory.Chip(awakened ? "覚醒" : promoted ? "成り" : "生還", awakened ? d.awakenColor : promoted ? Palette.EnemyLight : Palette.GoldLight);
            promo.style.marginLeft = 6;
            chips.Add(promo);
        }
        names.Add(chips);
        top.Add(names);
        detail.Add(top);

        // 生還すると攻撃+1・体力+1（AbilitySystem.ReturnAsVeteran）
        int atk = promoted ? d.promotedATK : d.baseATK + (veteran ? 1 : 0) + (awakened ? d.awakenedATK : 0);
        int def = promoted ? d.promotedDEF : d.baseDEF + (awakened ? d.awakenedDEF : 0);
        int hp = promoted ? d.promotedHP : d.baseHP + (veteran ? 1 : 0) + (awakened ? d.awakenedHP : 0);
        var stats = UIFactory.StatRow(atk, def, UIFactory.FormatHP(hp));
        stats.style.marginTop = 12;
        detail.Add(stats);

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.style.flexWrap = Wrap.Wrap;
        buttons.style.marginTop = 10;
        if (d.canPromote)
            buttons.Add(UIFactory.Button(promoted ? "成る前を見る" : "成った姿を見る", () => Select(d, !promoted)));
        if (d.canAwaken)
        {
            var ab = UIFactory.Button(awakened ? "覚醒する前を見る" : "覚醒した姿を見る", () => { if (awakened) Select(d, false); else SelectAwakened(d); });
            ab.style.marginLeft = buttons.childCount > 0 ? 8 : 0;
            buttons.Add(ab);
        }
        if (HasVeteran(d))
        {
            var vb = UIFactory.Button(veteran ? "生還する前を見る" : "生還した姿を見る", () => Select(d, false, !veteran));
            vb.style.marginLeft = 8;
            buttons.Add(vb);
        }
        if (buttons.childCount > 0) detail.Add(buttons);

        string desc = awakened ? d.awakenedDescription + "（練度★3の部員が、その局で活躍を" + BalanceTuning.AwakenActivities + "回重ねると覚醒する）"
            : promoted ? d.promotedDescription : (veteran ? d.veteranDescription : d.description);
        if (!string.IsNullOrEmpty(desc))
        {
            var p = UIFactory.Paragraph(desc, 19, Palette.Text);
            p.style.marginTop = 12;
            detail.Add(p);
        }

        string stages = StageList(d.pieceType);
        if (stages != null)
        {
            var s = UIFactory.Label("登場する局: " + stages, 17, Palette.TextSub);
            s.style.marginTop = 8;
            s.style.whiteSpace = WhiteSpace.Normal;
            detail.Add(s);
        }

        var moveTitle = UIFactory.Label(awakened ? "動き（覚醒）" : promoted ? "動き（成り）" : (veteran ? "動き（生還後）" : "動き"), 18, Palette.Gold, "c3-bold");
        moveTitle.style.marginTop = 16;
        moveTitle.style.marginBottom = 8;
        detail.Add(moveTitle);
        MoveDirection[] moves = veteran ? d.veteranMoveDirections
            : awakened && d.awakenedMoveDirections != null && d.awakenedMoveDirections.Length > 0 ? d.awakenedMoveDirections
            : UIFactory.MovesOf(d, promoted);
        detail.Add(UIFactory.MoveGrid(moves, false, 24f));
        Label immovable = UIFactory.ImmovableNote(moves);
        detail.Add(immovable != null ? immovable : UIFactory.MoveLegend());
    }

    /// <summary>敵の布陣に出てくる局（例: 「六・七・八」）。出てこなければ null</summary>
    private static string StageList(PieceType type)
    {
        if (type == PieceType.C3) return null;
        var list = new List<string>();
        for (int stage = 1; stage <= StageManager.MaxStages; stage++)
        {
            int count = 0;
            foreach (var p in StageManager.GetEnemyLayout(stage, StageManager.GetBoardSizeForStage(stage)))
                if (p.type == type) count++;
            if (count > 0) list.Add("第" + UIFactory.Kanji(stage) + "局" + (count > 1 ? "×" + count : ""));
        }
        return list.Count > 0 ? string.Join("・", list) : null;
    }
}
