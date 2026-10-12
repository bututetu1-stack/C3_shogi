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
    // オンライン: 部屋を作るとき（ルールを選んでから部屋を作る）
    private bool hostOnline;
    private string serverText;

    /// <summary>ルールを選ぶ画面から</summary>
    public static void Show(Action onDone, Action onBack)
    {
        Ensure();
        instance.onDone = onDone;
        instance.onBack = onBack;
        instance.ModeScreen();
    }

    /// <summary>待っているあいだの画面（相手の並べ方など。ほかの画面から使う）</summary>
    public static void ShowWaiting(string text)
    {
        Ensure();
        instance.Waiting(text, null);
    }

    public static void HideWaiting()
    {
        if (instance != null) instance.Close();
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
        // 画面ごとに中身の入れ物を作る（画面を切り替えると、その画面の見張り（schedule）も一緒に止まる）
        var screen = new VisualElement();
        screen.style.position = Position.Absolute;
        screen.style.left = screen.style.right = screen.style.top = screen.style.bottom = 0;
        screen.style.alignItems = Align.Center;
        screen.style.justifyContent = Justify.Center;
        root.Add(screen);
        return screen;
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

    // ------------------------------------------------------------
    // 1台で2人か、オンラインか
    // ------------------------------------------------------------

    private void ModeScreen()
    {
        hostOnline = false;
        VisualElement r = NewScreen();
        var title = UIFactory.Label("対人戦", 64, Palette.Text, "c3-mincho");
        title.style.letterSpacing = 16;
        r.Add(title);
        var sub = UIFactory.Label("部員どうしで対局します。", 20, Palette.TextSub);
        sub.style.marginBottom = 24;
        r.Add(sub);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.justifyContent = Justify.Center;
        row.Add(ModeCard("1台で2人", "同じパソコンで、交代で指す。", "これで遊ぶ", () => RuleScreen()));
        row.Add(ModeCard("部屋を作る", "オンラインで遊ぶ。ルールを選ぶと4桁の部屋コードが出るので、相手に伝える。", "部屋を作る", () =>
        {
            if (!ReadServer()) return;
            hostOnline = true;
            RuleScreen();
        }));
        row.Add(ModeCard("部屋に入る", "相手から聞いた部屋コードで入る。ルールは部屋を作った人が決める。", "部屋に入る", () =>
        {
            if (!ReadServer()) return;
            JoinScreen();
        }));
        r.Add(row);

        // サーバーのアドレス（オンラインだけ。前に入れたものを覚えておく）
        var server = new VisualElement();
        server.style.flexDirection = FlexDirection.Row;
        server.style.alignItems = Align.Center;
        server.style.marginTop = 22;
        server.Add(UIFactory.Label("オンラインのサーバー", 17, Palette.TextSub));
        var field = new TextField();
        field.name = "pvp-server";
        field.value = string.IsNullOrEmpty(serverText) ? PvpNet.SavedServer : serverText;
        field.style.width = 560;
        field.style.marginLeft = 12;
        field.style.fontSize = 17;
        field.RegisterValueChangedCallback(e => serverText = e.newValue);
        serverText = field.value;
        server.Add(field);
        r.Add(server);
        var hint = UIFactory.Label("例: wss://c3shogi-relay.〇〇.workers.dev（置き方は server/README.md）", 14, Palette.TextSub);
        hint.style.marginTop = 4;
        r.Add(hint);
        statusLabel = UIFactory.Label("", 17, Palette.EnemyLight);
        statusLabel.style.marginTop = 6;
        r.Add(statusLabel);

        var back = UIFactory.Button("タイトルへ戻る", () => { Close(); if (onBack != null) onBack(); });
        back.style.marginTop = 18;
        r.Add(back);
    }

    private Label statusLabel;

    private bool ReadServer()
    {
        string url = (serverText ?? "").Trim();
        if (!(url.StartsWith("ws://") || url.StartsWith("wss://") || url.StartsWith("http://") || url.StartsWith("https://")))
        {
            if (statusLabel != null) statusLabel.text = "サーバーのアドレスを入れてください（wss:// で始まるもの）";
            return false;
        }
        PvpNet.SavedServer = url;
        return true;
    }

    private VisualElement ModeCard(string name, string text, string button, Action onClick)
    {
        var card = UIFactory.Panel();
        card.style.width = 340;
        card.style.marginLeft = card.style.marginRight = 12;
        card.style.paddingTop = 20;
        card.style.paddingBottom = 20;
        card.style.paddingLeft = card.style.paddingRight = 20;
        card.style.alignItems = Align.Center;
        card.Add(UIFactory.Label(name, 30, Palette.GoldLight, "c3-mincho"));
        var p = UIFactory.Paragraph(text, 18, Palette.Text);
        p.style.marginTop = 8;
        p.style.marginBottom = 14;
        p.style.width = 290;
        card.Add(p);
        card.Add(UIFactory.Button(button, onClick, "c3-button--primary"));
        return card;
    }

    // ------------------------------------------------------------
    // オンライン: 部屋に入る・待つ
    // ------------------------------------------------------------

    private void JoinScreen()
    {
        VisualElement r = NewScreen();
        r.Add(UIFactory.Label("部屋に入る", 48, Palette.Text, "c3-mincho"));
        var sub = UIFactory.Label("相手から聞いた4桁の部屋コードを入れてください", 20, Palette.TextSub);
        sub.style.marginTop = 8;
        sub.style.marginBottom = 16;
        r.Add(sub);
        var field = new TextField();
        field.name = "pvp-code";
        field.maxLength = 8;
        field.style.width = 240;
        field.style.fontSize = 40;
        r.Add(field);
        var status = UIFactory.Label("", 18, Palette.EnemyLight);
        status.style.marginTop = 8;
        r.Add(status);
        var go = UIFactory.Button("入る", () =>
        {
            string code = (field.value ?? "").Trim().ToUpperInvariant();
            if (code.Length < 4) { status.text = "部屋コードは4桁です"; return; }
            Connect(code);
            Waiting("部屋 " + code + " に入っています…", () =>
            {
                var online = PvpOnline.Instance;
                if (online != null && online.Setup != null) { StartOnline(); return true; }
                return false;
            });
        }, "c3-button--primary", "c3-button--big");
        go.style.marginTop = 12;
        r.Add(go);
        var back = UIFactory.Button("戻る", ModeScreen);
        back.style.marginTop = 16;
        r.Add(back);
    }

    private void Connect(string code)
    {
        var net = PvpNet.Ensure();
        PvpOnline.Ensure(net);
        net.Connect(PvpNet.SavedServer, code);
    }

    /// <summary>待つ画面。check が true を返したら次へ進む（check は自分で次の画面を出す）</summary>
    private void Waiting(string text, Func<bool> check)
    {
        VisualElement r = NewScreen();
        var label = UIFactory.Label(text, 30, Palette.Text, "c3-mincho");
        r.Add(label);
        var status = UIFactory.Label("", 18, Palette.TextSub);
        status.style.marginTop = 10;
        r.Add(status);
        var back = UIFactory.Button("やめてタイトルへ戻る", () =>
        {
            if (PvpOnline.Instance != null) PvpOnline.Instance.Leave();
            Close();
            if (onBack != null) onBack();
        });
        back.style.marginTop = 24;
        r.Add(back);
        IVisualElementScheduledItem watch = null;
        watch = r.schedule.Execute(() =>
        {
            var net = PvpNet.Instance;
            if (net != null)
            {
                status.text = !string.IsNullOrEmpty(net.LastError) && !net.Connected ? "つながりません: " + net.LastError
                    : !net.Connected ? "サーバーにつないでいます…"
                    : net.PeerConnected ? "相手とつながっています" : "相手を待っています";
            }
            if (check != null && check()) watch.Pause();   // 次へ進んだら見張りをやめる
        }).Every(200);
    }

    private void HostWaiting(PvpMatch.Rule rule)
    {
        string code = PvpNet.NewRoomCode();
        Connect(code);
        VisualElement r = NewScreen();
        r.Add(UIFactory.Label("部屋コード", 28, Palette.TextSub, "c3-mincho"));
        var codeLabel = UIFactory.Label(code, 120, Palette.GoldLight, "c3-mincho");
        codeLabel.style.letterSpacing = 24;
        r.Add(codeLabel);
        var how = UIFactory.Label("このコードを相手に伝えて、「部屋に入る」で入ってもらってください（ルール: "
            + (rule == PvpMatch.Rule.Cost ? "コスト制" : "ランダム配布") + "）", 20, Palette.Text);
        how.style.marginTop = 6;
        r.Add(how);
        var status = UIFactory.Label("サーバーにつないでいます…", 18, Palette.TextSub);
        status.style.marginTop = 12;
        r.Add(status);
        var back = UIFactory.Button("やめてタイトルへ戻る", () =>
        {
            if (PvpOnline.Instance != null) PvpOnline.Instance.Leave();
            Close();
            if (onBack != null) onBack();
        });
        back.style.marginTop = 24;
        r.Add(back);
        bool sent = false;
        IVisualElementScheduledItem watch = null;
        watch = r.schedule.Execute(() =>
        {
            if (sent) { watch.Pause(); return; }
            var net = PvpNet.Instance;
            var online = PvpOnline.Instance;
            if (net == null || online == null || sent) return;
            status.text = !net.Connected ? (!string.IsNullOrEmpty(net.LastError) ? "つながりません: " + net.LastError : "サーバーにつないでいます…")
                : net.PeerConnected ? "相手が入りました" : "相手を待っています";
            if (net.Connected && net.Seat == 0 && net.PeerConnected)
            {
                sent = true;
                online.SendSetup(rule);
                StartOnline();
            }
            else if (net.Seat == 1 && online.Setup != null)
            {
                sent = true;   // 同じコードの部屋がすでにあった（入る側になった）
                StartOnline();
            }
        }).Every(200);
    }

    /// <summary>ルール・種・先手が決まったら、自分の編成へ</summary>
    private void StartOnline()
    {
        var net = PvpNet.Instance;
        var online = PvpOnline.Instance;
        PvpNet.Msg setup = online.Setup;
        PvpMatch.Rule rule = setup.rule == "Random" ? PvpMatch.Rule.Random : PvpMatch.Rule.Cost;
        Team local = net.Seat == setup.first ? Team.Player : Team.Enemy;
        PvpMatch.BeginOnline(rule, setup.seed, local);
        if (rule == PvpMatch.Rule.Random) BuildRounds();
        Team foe = local == Team.Player ? Team.Enemy : Team.Player;
        BuildSide(local, () =>
        {
            online.SendTeam(PvpMatch.SideOf(local));
            Waiting("相手の編成を待っています", () =>
            {
                if (online.PeerTeam == null) return false;
                PvpOnline.ApplyTeam(online.PeerTeam, PvpMatch.SideOf(foe));
                Summary();
                return true;
            });
        });
    }

    private void RuleScreen()
    {
        VisualElement r = NewScreen();
        var title = UIFactory.Label("対人戦", 64, Palette.Text, "c3-mincho");
        title.style.letterSpacing = 16;
        r.Add(title);
        var sub = UIFactory.Label(hostOnline ? "ルールを選ぶと部屋ができます。先手と後手は、相手が入ったときにランダムで決まります。"
            : "1台で2人が交代で指します。先手は盤の下、後手は盤の上。", 20, Palette.TextSub);
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

        var back = UIFactory.Button("戻る", ModeScreen);
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
        card.Add(UIFactory.Button("このルールで遊ぶ", () => { if (hostOnline) HostWaiting(rule); else BeginRule(rule); }, "c3-button--primary", "c3-button--big"));
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
        var t = UIFactory.Label(PvpMatch.Online ? "2人の編成がそろいました（あなたは" + PvpMatch.SideLong(PvpMatch.LocalTeam) + "）" : "2人の編成がそろいました", 40, Palette.Text, "c3-mincho");
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
        var start = UIFactory.Button(PvpMatch.Online ? "並べ方へ" : "対局を始める", () =>
        {
            Close();
            if (onDone != null) onDone();
        }, "c3-button--primary", "c3-button--big");
        start.style.marginTop = 10;
        r.Add(start);
    }
}
