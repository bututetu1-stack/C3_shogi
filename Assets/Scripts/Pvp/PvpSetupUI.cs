using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 対人戦の準備の画面: ルールを選ぶ → 先手の編成 → （交代）→ 後手の編成 → 2人の編成を見て対局へ。
/// 1台で2人が交代で使うので、編成の前に「相手は画面を見ないで」の合図を出す。
/// </summary>
public class PvpSetupUI : MonoBehaviour
{
    private static PvpSetupUI instance;
    private UIDocument uiDocument;
    private VisualElement root;
    private Action onDone;
    private Action onBack;

    // コスト制の編成で、いま説明を出している札
    private PieceData focusPiece;
    private UpgradeKind? focusUpgrade;
    // ランダム配布の候補（2人に同じものを出す）
    private List<List<DraftOption>> rounds;

    /// <summary>ルールを選ぶ画面から</summary>
    public static void Show(Action onDone, Action onBack)
    {
        Ensure();
        instance.onDone = onDone;
        instance.onBack = onBack;
        instance.RuleScreen();
    }

    /// <summary>ルールを決めた状態で、先手の編成から（決着画面の「もう一度」）</summary>
    public static void ShowRule(PvpMatch.Rule rule, Action onDone, Action onBack)
    {
        Ensure();
        instance.onDone = onDone;
        instance.onBack = onBack;
        instance.BeginRule(rule);
    }

    private static void Ensure()
    {
        if (instance != null) return;
        var obj = new GameObject("PvpSetupUI");
        instance = obj.AddComponent<PvpSetupUI>();
        instance.uiDocument = obj.AddComponent<UIDocument>();
        foreach (var d in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            if (d != instance.uiDocument && d.panelSettings != null)
            {
                instance.uiDocument.panelSettings = d.panelSettings;
                break;
            }
        }
        instance.uiDocument.sortingOrder = 92;
    }

    private VisualElement NewScreen()
    {
        root = UIFactory.SetupRoot(uiDocument);
        root.Clear();
        root.pickingMode = PickingMode.Position;
        root.style.backgroundColor = new Color(0.03f, 0.025f, 0.02f, 1f);
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;
        return root;
    }

    private void Close()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            uiDocument.rootVisualElement.Clear();
            uiDocument.rootVisualElement.pickingMode = PickingMode.Ignore;
            uiDocument.rootVisualElement.style.backgroundColor = new Color(0, 0, 0, 0);
        }
    }

    // ------------------------------------------------------------
    // ルールを選ぶ
    // ------------------------------------------------------------

    private void RuleScreen()
    {
        VisualElement r = NewScreen();
        var title = UIFactory.Label("対人戦", 64, Palette.Text, "c3-mincho");
        title.style.letterSpacing = 16;
        r.Add(title);
        var sub = UIFactory.Label("1台で2人が交代で指します。先手は盤の下、後手は盤の上。", 20, Palette.TextSub);
        sub.style.marginBottom = 28;
        r.Add(sub);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.justifyContent = Justify.Center;
        row.Add(RuleCard("コスト制", "持ち点" + PvpMatch.Budget + "の中で、部員・将棋の駒・敵の駒・全軍強化を自由に選ぶ。"
            + "\n部員は激レア9・レア6・ブロンズ4。全軍強化は" + PvpMatch.UpgradeCost + "（" + PvpMatch.MaxUpgrades + "枚まで）。", PvpMatch.Rule.Cost));
        row.Add(RuleCard("ランダム配布", "3枚から1枚を選ぶのを" + PvpMatch.RandomRounds + "回。"
            + "\n2人に同じ候補が出るので、選び方の腕の勝負。", PvpMatch.Rule.Random));
        r.Add(row);

        var back = UIFactory.Button("タイトルへ戻る", () => { Close(); if (onBack != null) onBack(); });
        back.style.marginTop = 24;
        r.Add(back);
    }

    private VisualElement RuleCard(string name, string text, PvpMatch.Rule rule)
    {
        var card = UIFactory.Panel();
        card.style.width = 420;
        card.style.marginLeft = card.style.marginRight = 14;
        card.style.paddingTop = 22;
        card.style.paddingBottom = 22;
        card.style.paddingLeft = card.style.paddingRight = 24;
        card.style.alignItems = Align.Center;
        card.Add(UIFactory.Label(name, 34, Palette.GoldLight, "c3-mincho"));
        var p = UIFactory.Paragraph(text, 19, Palette.Text);
        p.style.marginTop = 10;
        p.style.marginBottom = 16;
        p.style.width = 360;
        card.Add(p);
        card.Add(UIFactory.Button("このルールで遊ぶ", () => BeginRule(rule), "c3-button--primary", "c3-button--big"));
        return card;
    }

    private void BeginRule(PvpMatch.Rule rule)
    {
        PvpMatch.Begin(rule);
        if (rule == PvpMatch.Rule.Random) BuildRounds();
        Handoff(Team.Player, () => BuildSide(Team.Player, () => Handoff(Team.Enemy, () => BuildSide(Team.Enemy, Summary))));
    }

    private void BuildSide(Team team, Action next)
    {
        if (PvpMatch.CurrentRule == PvpMatch.Rule.Cost) CostBuilder(team, next);
        else RandomDraft(team, 0, next);
    }

    // ------------------------------------------------------------
    // 交代の合図
    // ------------------------------------------------------------

    /// <summary>交代の合図（並べ方の前など、ほかの画面から使う）。「準備ができた」で閉じて next</summary>
    public static void ShowHandoff(Team team, string what, Action next)
    {
        Ensure();
        instance.Handoff(team, () => { instance.Close(); if (next != null) next(); }, what);
    }

    private void Handoff(Team team, Action next, string what = "の編成")
    {
        VisualElement r = NewScreen();
        Color c = team == Team.Player ? Palette.PlayerLight : Palette.EnemyLight;
        var t = UIFactory.Label(PvpMatch.SideLong(team) + what, 56, c, "c3-mincho");
        t.style.letterSpacing = 8;
        r.Add(t);
        var other = team == Team.Player ? Team.Enemy : Team.Player;
        var s = UIFactory.Label(PvpMatch.SideName(other) + "の人は、画面を見ないでください。", 24, Palette.Text);
        s.style.marginTop = 12;
        s.style.marginBottom = 28;
        r.Add(s);
        r.Add(UIFactory.Button("準備ができた", next, "c3-button--primary", "c3-button--big"));
    }

    // ------------------------------------------------------------
    // コスト制
    // ------------------------------------------------------------

    private void CostBuilder(Team team, Action next)
    {
        PvpMatch.Side side = PvpMatch.SideOf(team);
        VisualElement r = NewScreen();

        var panel = UIFactory.Panel();
        panel.style.width = Length.Percent(96);
        panel.style.maxWidth = 1600;
        panel.style.height = Length.Percent(94);
        panel.style.paddingLeft = panel.style.paddingRight = 22;
        r.Add(panel);

        // 見出しと持ち点
        var head = new VisualElement();
        head.style.flexDirection = FlexDirection.Row;
        head.style.alignItems = Align.Center;
        head.style.marginBottom = 8;
        Color sideColor = team == Team.Player ? Palette.PlayerLight : Palette.EnemyLight;
        var title = UIFactory.Label(PvpMatch.SideLong(team) + "の編成", 30, sideColor, "c3-mincho");
        title.style.marginRight = 24;
        head.Add(title);
        int left = PvpMatch.Budget - side.CostUsed();
        head.Add(UIFactory.Label("持ち点 " + PvpMatch.Budget + " のうち 残り " + left, 26, left > 0 ? Palette.GoldLight : Palette.TextSub, "c3-bold"));
        panel.Add(head);

        var body = new VisualElement();
        body.style.flexDirection = FlexDirection.Row;
        body.style.flexGrow = 1;
        body.style.flexShrink = 1;
        body.style.minHeight = 0;
        panel.Add(body);

        // 左: 選べる札
        var catalog = UIFactory.Scroll();
        catalog.style.flexGrow = 1;
        catalog.style.flexShrink = 1;
        catalog.style.flexBasis = 0;
        body.Add(catalog);
        AddGroup(catalog, "部員（1人1枚まで）", PvpMatch.Members, side, team, next);
        AddGroup(catalog, "将棋の駒（何枚でも）", PvpMatch.StandardPieces, side, team, next);
        AddGroup(catalog, "敵の駒（結界は1枚まで）", PvpMatch.EnemyPieces, side, team, next);
        var upHead = UIFactory.Label("全軍強化（" + PvpMatch.MaxUpgrades + "枚まで・各" + PvpMatch.UpgradeCost + "）", 20, Palette.Gold, "c3-bold");
        upHead.style.marginTop = 12;
        catalog.Add(upHead);
        var upRow = CardRow();
        foreach (var u in PvpMatch.Upgrades)
        {
            UpgradeKind kind = u;
            var opt = DraftOption.Upgrade(kind);
            upRow.Add(SmallCard(UpgradeIcon(opt), opt.Title, PvpMatch.UpgradeCost, PvpMatch.CannotAddUpgrade(side) == null,
                focusUpgrade == kind, () => { focusUpgrade = kind; focusPiece = null; CostBuilder(team, next); }));
        }
        catalog.Add(upRow);

        // 右: 選んだ駒と説明
        var right = new VisualElement();
        right.style.width = 520;
        right.style.flexShrink = 0;
        right.style.marginLeft = 18;
        right.style.paddingLeft = 18;
        right.style.borderLeftWidth = 1;
        right.style.borderLeftColor = Palette.PanelBorder;
        body.Add(right);

        right.Add(UIFactory.Label("選んだ駒（" + side.pieces.Count + "/" + PvpMatch.MaxPieces + "）", 20, Palette.Gold, "c3-bold"));
        var chosen = UIFactory.Scroll();
        chosen.style.height = 230;
        chosen.style.flexShrink = 0;
        chosen.contentContainer.style.flexDirection = FlexDirection.Row;
        chosen.contentContainer.style.flexWrap = Wrap.Wrap;
        for (int i = 0; i < side.pieces.Count; i++)
        {
            int index = i;
            PieceData d = side.pieces[i];
            chosen.Add(ChosenChip(UIFactory.PieceIcon(d, false, 40), CardName(d), PvpMatch.CostOf(d), () =>
            {
                side.pieces.RemoveAt(index);
                CostBuilder(team, next);
            }));
        }
        for (int i = 0; i < side.upgrades.Count; i++)
        {
            int index = i;
            var opt = DraftOption.Upgrade(side.upgrades[i]);
            chosen.Add(ChosenChip(UpgradeIcon(opt, 40), opt.Title, PvpMatch.UpgradeCost, () =>
            {
                side.upgrades.RemoveAt(index);
                CostBuilder(team, next);
            }));
        }
        right.Add(chosen);
        var hint = UIFactory.Label("選んだ駒をクリックすると外せます", 14, Palette.TextSub);
        hint.style.marginBottom = 8;
        right.Add(hint);

        // 説明と「加える」
        var detail = UIFactory.Scroll();
        detail.style.flexGrow = 1;
        detail.style.flexShrink = 1;
        right.Add(detail);
        RenderFocus(detail, side, team, next);

        var done = UIFactory.Button("この編成で決める", () =>
        {
            focusPiece = null;
            focusUpgrade = null;
            next();
        }, "c3-button--primary", "c3-button--big");
        done.SetEnabled(side.pieces.Count > 0);
        done.style.marginTop = 10;
        right.Add(done);
    }

    private void AddGroup(VisualElement parent, string heading, PieceType[] types, PvpMatch.Side side, Team team, Action next)
    {
        var h = UIFactory.Label(heading, 20, Palette.Gold, "c3-bold");
        h.style.marginTop = 12;
        parent.Add(h);
        var row = CardRow();
        foreach (var t in types)
        {
            PieceData d = BoardManager.Instance != null ? BoardManager.Instance.GetPieceDataByType(t) : null;
            if (d == null) continue;
            PieceData data = d;
            row.Add(SmallCard(UIFactory.PieceIcon(d, false, 58), CardName(d), PvpMatch.CostOf(d), PvpMatch.CannotAdd(side, d) == null,
                focusPiece == d, () => { focusPiece = data; focusUpgrade = null; CostBuilder(team, next); }));
        }
        parent.Add(row);
    }

    private static VisualElement CardRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginTop = 6;
        return row;
    }

    private static VisualElement SmallCard(VisualElement icon, string name, int cost, bool enabled, bool focused, Action onClick)
    {
        var card = new VisualElement();
        card.AddToClassList("c3-card");
        card.EnableInClassList("c3-card--selected", focused);
        card.style.width = 118;
        card.style.marginRight = 8;
        card.style.marginBottom = 8;
        card.style.paddingTop = 8;
        card.style.paddingBottom = 8;
        card.style.opacity = enabled ? 1f : 0.45f;
        card.Add(icon);
        var label = UIFactory.Label(name, 17, Palette.Text, "c3-mincho");
        label.style.marginTop = 4;
        card.Add(label);
        var chip = UIFactory.Chip("コスト " + cost, Palette.GoldLight);
        chip.style.marginTop = 4;
        card.Add(chip);
        card.RegisterCallback<ClickEvent>(e => onClick());
        return card;
    }

    private static VisualElement ChosenChip(VisualElement icon, string name, int cost, Action onRemove)
    {
        var chip = new VisualElement();
        chip.AddToClassList("c3-card");
        chip.style.width = 92;
        chip.style.marginRight = 6;
        chip.style.marginBottom = 6;
        chip.style.paddingTop = 4;
        chip.style.paddingBottom = 4;
        chip.Add(icon);
        chip.Add(UIFactory.Label(name, 13, Palette.Text));
        chip.Add(UIFactory.Label(cost.ToString(), 12, Palette.GoldLight));
        chip.RegisterCallback<ClickEvent>(e => onRemove());
        return chip;
    }

    private static VisualElement UpgradeIcon(DraftOption opt, float size = 58)
    {
        var icon = new VisualElement();
        icon.style.width = size;
        icon.style.height = size;
        icon.style.alignItems = Align.Center;
        icon.style.justifyContent = Justify.Center;
        icon.style.borderTopLeftRadius = icon.style.borderTopRightRadius = icon.style.borderBottomLeftRadius = icon.style.borderBottomRightRadius = size / 2;
        Color c = opt.GlyphColor;
        icon.style.backgroundColor = new Color(c.r, c.g, c.b, 0.22f);
        icon.style.borderTopWidth = icon.style.borderBottomWidth = icon.style.borderLeftWidth = icon.style.borderRightWidth = 2;
        icon.style.borderTopColor = icon.style.borderBottomColor = icon.style.borderLeftColor = icon.style.borderRightColor = c;
        icon.pickingMode = PickingMode.Ignore;
        icon.Add(UIFactory.Label(opt.Glyph, size * 0.5f, Color.Lerp(c, Color.white, 0.4f), "c3-mincho"));
        return icon;
    }

    private void RenderFocus(VisualElement detail, PvpMatch.Side side, Team team, Action next)
    {
        if (focusPiece != null)
        {
            PieceData d = focusPiece;
            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.Add(UIFactory.Label(d.pieceName, 26, Palette.Text, "c3-mincho"));
            var chip = UIFactory.Chip(Palette.RarityName(d.rarity), Palette.RarityLabel(d.rarity));
            chip.style.marginLeft = 10;
            head.Add(chip);
            detail.Add(head);
            detail.Add(UIFactory.StatRow(d.baseATK + PiecePool.RecruitBonusATK(d), d.baseDEF, UIFactory.FormatHP(d.baseHP + PiecePool.RecruitBonusHP(d))));
            string why = PvpMatch.CannotAdd(side, d);
            var add = UIFactory.Button(why == null ? "加える（コスト " + PvpMatch.CostOf(d) + "）" : "加えられない（" + why + "）", () =>
            {
                if (PvpMatch.CannotAdd(side, d) != null) return;
                side.pieces.Add(d);
                CostBuilder(team, next);
            });
            add.SetEnabled(why == null);
            add.style.alignSelf = Align.FlexStart;
            add.style.marginTop = 10;
            detail.Add(add);
            detail.Add(UIFactory.PieceText(UIFactory.FlavorOf(d, false), d.description, 17));
        }
        else if (focusUpgrade.HasValue)
        {
            var opt = DraftOption.Upgrade(focusUpgrade.Value);
            detail.Add(UIFactory.Label(opt.Title, 26, Palette.Text, "c3-mincho"));
            var p = UIFactory.Paragraph(opt.Description.Replace("最後まで続く", "この対局のあいだ続く"), 18, Palette.Text);
            p.style.marginTop = 6;
            detail.Add(p);
            string why = PvpMatch.CannotAddUpgrade(side);
            UpgradeKind kind = focusUpgrade.Value;
            var add = UIFactory.Button(why == null ? "加える（コスト " + PvpMatch.UpgradeCost + "）" : "加えられない（" + why + "）", () =>
            {
                if (PvpMatch.CannotAddUpgrade(side) != null) return;
                side.upgrades.Add(kind);
                CostBuilder(team, next);
            });
            add.SetEnabled(why == null);
            add.style.alignSelf = Align.FlexStart;
            add.style.marginTop = 10;
            detail.Add(add);
        }
        else
        {
            detail.Add(UIFactory.Paragraph("左の札をクリックすると、ここに説明と「加える」が出ます。駒は自陣（手前の3段）に、選んだ順に並びます。", 17, Palette.TextSub));
        }
    }

    private static string CardName(PieceData d)
    {
        return string.IsNullOrEmpty(d.displayName) ? d.pieceName : d.displayName;
    }

    // ------------------------------------------------------------
    // ランダム配布（2人に同じ候補）
    // ------------------------------------------------------------

    private void BuildRounds()
    {
        rounds = new List<List<DraftOption>>();
        var offered = new List<PieceData>();   // 同じ部員が2回出ないように
        PieceData[] all = BoardManager.Instance != null ? BoardManager.Instance.allPieceData : new PieceData[0];
        for (int r = 0; r < PvpMatch.RandomRounds; r++)
        {
            var options = PiecePool.DrawOptions(all, 3, offered, PvpMatch.RandomDraftStage, true, null);
            // 対人戦で使わない強化（応急修理・歩兵を消す）は、使える強化に差し替える
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].piece != null) { offered.Add(options[i].piece); continue; }
                if (options[i].train != null || Array.IndexOf(PvpMatch.Upgrades, options[i].upgrade) < 0)
                    options[i] = DraftOption.Upgrade(PvpMatch.Upgrades[GameRng.Range(0, PvpMatch.Upgrades.Length)]);
            }
            rounds.Add(options);
        }
    }

    private void RandomDraft(Team team, int round, Action next)
    {
        if (round >= rounds.Count)
        {
            PieceSelectionUI.HeaderOverride = null;
            next();
            return;
        }
        Close();
        var sel = FindFirstObjectByType<PieceSelectionUI>();
        if (sel == null) { next(); return; }
        PvpMatch.Side side = PvpMatch.SideOf(team);
        PieceSelectionUI.HeaderOverride = "対人戦　" + PvpMatch.SideLong(team) + "の編成（ランダム配布）";
        sel.ShowSelection(rounds[round], option =>
        {
            if (option != null)
            {
                if (option.piece != null) side.pieces.Add(option.piece);
                else if (option.train == null) side.upgrades.Add(option.upgrade);
            }
            RandomDraft(team, round + 1, next);
        }, null, 0, round + 1, rounds.Count);
    }

    // ------------------------------------------------------------
    // 2人の編成を見て対局へ
    // ------------------------------------------------------------

    private void Summary()
    {
        VisualElement r = NewScreen();
        var t = UIFactory.Label("2人の編成がそろいました", 40, Palette.Text, "c3-mincho");
        t.style.marginBottom = 18;
        r.Add(t);
        foreach (var side in new[] { PvpMatch.First, PvpMatch.Second })
        {
            Color c = side.team == Team.Player ? Palette.PlayerLight : Palette.EnemyLight;
            string extra = side.upgrades.Count > 0 ? "　全軍強化: " + string.Join("・", side.upgrades.ConvertAll(u => DraftOption.Upgrade(u).Title)) : "";
            r.Add(UIFactory.Label(PvpMatch.SideLong(side.team) + extra, 20, c, "c3-bold"));
            var row = CardRow();
            row.style.justifyContent = Justify.Center;
            row.style.marginBottom = 14;
            foreach (var d in side.pieces)
            {
                var icon = UIFactory.PieceIcon(d, false, 52);
                icon.style.marginLeft = icon.style.marginRight = 3;
                row.Add(icon);
            }
            r.Add(row);
        }
        var start = UIFactory.Button("対局を始める", () =>
        {
            Close();
            if (onDone != null) onDone();
        }, "c3-button--primary", "c3-button--big");
        start.style.marginTop = 10;
        r.Add(start);
    }
}
