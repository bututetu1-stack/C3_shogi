using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>タイトル画面（はじめる・駒一覧・遊び方・設定）</summary>
public class TitleScreenUI : MonoBehaviour
{

    private static TitleScreenUI instance;
    private UIDocument uiDocument;
    private Action onStart;
    private Action onContinue;
    private VisualElement helpOverlay;

    public static void Show(Action onStart, Action onContinue = null)
    {
        if (instance == null)
        {
            var obj = new GameObject("TitleScreenUI");
            instance = obj.AddComponent<TitleScreenUI>();
            instance.uiDocument = obj.AddComponent<UIDocument>();
            foreach (var d in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (d != instance.uiDocument && d.panelSettings != null)
                {
                    instance.uiDocument.panelSettings = d.panelSettings;
                    break;
                }
            }
            instance.uiDocument.sortingOrder = 90;
        }
        instance.onStart = onStart;
        instance.onContinue = onContinue;
        instance.Build();
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayBGM("Title");
    }

    private void Build()
    {
        VisualElement root = UIFactory.SetupRoot(uiDocument);
        root.pickingMode = PickingMode.Position;
        root.style.backgroundColor = Palette.Background;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;

        // 背景（イラストがあればそれを使う）
        Texture2D art = Resources.Load<Texture2D>("Art/TitleBackground");
        var bg = new VisualElement();
        bg.style.position = Position.Absolute;
        bg.style.left = bg.style.right = bg.style.top = bg.style.bottom = 0;
        bg.pickingMode = PickingMode.Ignore;
        if (art != null)
        {
            bg.style.backgroundImage = new StyleBackground(art);
            bg.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
        }
        else
        {
            bg.style.backgroundImage = new StyleBackground(SpriteFactory.BackgroundGlow.texture);
            bg.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            bg.style.unityBackgroundImageTintColor = new Color(Palette.BackgroundGlow.r, Palette.BackgroundGlow.g, Palette.BackgroundGlow.b, 0.9f);

            // うっすらと大きな駒の影
            var piece = new VisualElement();
            piece.style.position = Position.Absolute;
            piece.style.width = 620;
            piece.style.height = 620;
            piece.style.left = Length.Percent(50);
            piece.style.top = Length.Percent(50);
            piece.style.translate = new Translate(Length.Percent(-50), Length.Percent(-56));
            piece.style.backgroundImage = new StyleBackground(SpriteFactory.PieceTexture(Rarity.Normal));
            piece.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            piece.style.opacity = 0.07f;
            piece.pickingMode = PickingMode.Ignore;
            bg.Add(piece);
        }
        root.Add(bg);

        var column = new VisualElement();
        column.style.alignItems = Align.Center;
        column.pickingMode = PickingMode.Ignore;
        root.Add(column);

        var title = UIFactory.Label("C3将棋", 128, Palette.Text, "c3-mincho");
        title.style.letterSpacing = 24;
        column.Add(title);

        var line = new VisualElement();
        line.style.width = 420;
        line.style.height = 2;
        line.style.backgroundColor = Palette.Gold;
        line.style.marginTop = 4;
        line.style.marginBottom = 12;
        column.Add(line);

        var subtitle = UIFactory.Label("C3部員たちの盤上ローグライク", 22, Palette.Gold, "c3-mincho");
        subtitle.style.letterSpacing = 6;
        subtitle.style.marginBottom = 48;
        column.Add(subtitle);

        // 中断した周があれば「続きから」を先に出す
        RunSaveData saved = onContinue != null ? RunSave.Load() : null;
        if (saved != null)
        {
            var resume = UIFactory.Button("続きから（第" + UIFactory.Kanji(Mathf.Max(1, saved.stage)) + "局）", OnContinueClicked, "c3-button--primary", "c3-button--big");
            resume.style.width = 320;
            resume.style.marginBottom = 14;
            column.Add(resume);
        }

        var start = UIFactory.Button(saved != null ? "はじめから" : "対局を始める", OnStartClicked, saved != null ? "c3-button--big" : "c3-button--primary", "c3-button--big");
        start.style.width = 320;
        start.style.marginBottom = 14;
        column.Add(start);

        var catalog = UIFactory.Button("駒一覧", PieceCatalogUI.Open);
        catalog.style.width = 320;
        catalog.style.marginBottom = 14;
        column.Add(catalog);

        var help = UIFactory.Button("遊び方", ShowHelp);
        help.style.width = 320;
        help.style.marginBottom = 14;
        column.Add(help);

        var settings = UIFactory.Button("設定", SettingsUI.Open);
        settings.style.width = 320;
        column.Add(settings);

        var credit = UIFactory.Label("フォント: しっぽり明朝 B1 / Zen角ゴシック New（SIL Open Font License）", 12, new Color(1f, 1f, 1f, 0.35f));
        credit.style.position = Position.Absolute;
        credit.style.bottom = 14;
        credit.style.right = 18;
        root.Add(credit);
    }

    private void OnContinueClicked()
    {
        Action resume = onContinue;
        onStart = null;
        onContinue = null;
        ClearTitle();
        if (resume != null) resume();
    }

    private void ClearTitle()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            uiDocument.rootVisualElement.Clear();
            uiDocument.rootVisualElement.pickingMode = PickingMode.Ignore;
            uiDocument.rootVisualElement.style.backgroundColor = new Color(0, 0, 0, 0);
        }
    }

    private void OnStartClicked()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            uiDocument.rootVisualElement.Clear();
            uiDocument.rootVisualElement.pickingMode = PickingMode.Ignore;
            uiDocument.rootVisualElement.style.backgroundColor = new Color(0, 0, 0, 0);
        }
        Action start = onStart;
        onStart = null;
        if (start != null) start();
    }

    // ------------------------------------------------------------
    // 遊び方
    // ------------------------------------------------------------

    private void ShowHelp()
    {
        VisualElement root = uiDocument.rootVisualElement;
        if (helpOverlay != null) helpOverlay.RemoveFromHierarchy();

        helpOverlay = new VisualElement();
        helpOverlay.style.position = Position.Absolute;
        helpOverlay.style.left = helpOverlay.style.right = helpOverlay.style.top = helpOverlay.style.bottom = 0;
        helpOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.7f);
        helpOverlay.style.alignItems = Align.Center;
        helpOverlay.style.justifyContent = Justify.Center;
        root.Add(helpOverlay);

        var panel = UIFactory.Panel();
        panel.style.width = Length.Percent(80);
        panel.style.maxWidth = 760;
        panel.style.maxHeight = Length.Percent(86);
        panel.style.paddingLeft = 28;
        panel.style.paddingRight = 20;
        helpOverlay.Add(panel);

        panel.Add(UIFactory.PanelTitle("遊び方"));
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexShrink = 1;
        panel.Add(scroll);

        AddSection(scroll, "勝ち負け", "相手の「C3」を倒せば、その局は勝利。自分の「C3」が倒されると敗北です。全十五局を勝ち抜きましょう。");
        AddSection(scroll, "動かし方", "自分の駒をクリックすると、動けるマスに点が表示されます。点をクリックすると移動します。橙の点は敵に狙われるマス、赤い枠は攻撃できる敵です。");
        AddSection(scroll, "戦い", "ダメージは「攻撃力 − 防御力」。体力が0になった駒は撃破されます。倒しきれなかったときは、攻撃した駒はその場に留まります。");
        AddSection(scroll, "成り", "敵陣（奥の2〜3段）に入るか、敵陣から出ると、駒は必ず成ります。SNや小錦のように、成ると退場してしまう駒もいるので注意。");
        AddSection(scroll, "仲間と強化", "各局の前に、新しい仲間か全軍の強化をひとつ選びます。仲間は次の局にも連れて行けます。選び直しは1局につき1回まで。第三局からは「〇〇を鍛える」札も出ます。");
        AddSection(scroll, "練度と★", "仲間にした部員は、周のあいだ「練度」をためて★（★3まで）に育ちます。局が変わっても練度は残ります。"
            + "\n・練度がたまること: " + RunRoster.XpSourcesText() + "。部の時間の「合宿」「練習試合」でもたまります。"
            + "\n・★の効果: ★1 " + RunRoster.StarEffectText(1) + "、★2 " + RunRoster.StarEffectText(2) + "、★3 " + RunRoster.StarEffectText(3)
            + "。★が上がるとその場で付き、次の局からは最初から付いています。"
            + "\n・★に必要な練度: ブロンズ " + RunRoster.ThresholdText(Rarity.Bronze) + "、レア " + RunRoster.ThresholdText(Rarity.Rare)
            + "、激レア " + RunRoster.ThresholdText(Rarity.SuperRare) + "（激レアほど育つのが遅い大器晩成）。"
            + "\n・駒の左下の★と、駒をクリックしたときの「練度 6/10（あと4）」で確かめられます。");
        AddSection(scroll, "覚醒", "★3の部員は、局の中で「活躍」を" + BalanceTuning.AwakenActivities + "回重ねると、その場で覚醒して上の姿になります（カットインが出ます）。"
            + "\n・活躍: 敵を倒すこと、またはその部員の能力が決まること（部員ごとの活躍は下の一覧。駒の情報にも出ます）。"
            + "\n・覚醒すると数値が上がり、能力が強くなります。小錦は「横綱」、僕は「後方腕組の極み」など、どんな姿になるかはタイトルの「駒一覧」で見られます。"
            + "\n・覚醒はその局のあいだだけ。次の局も★3のままなので、また活躍を" + BalanceTuning.AwakenActivities + "回重ねれば覚醒します。"
            + "\n・SN は覚醒しません（先代部長）。物鉄は★3になると、着任した艦娘が最初から改になります。"
            + "\n・敵の雷帝・龍神・魔王は、体力が半分を切ると第二形態になります。");
        AddSection(scroll, "部員ごとの活躍", FeatList());
        AddSection(scroll, "挑発と組み止め", "小錦を攻撃できる敵は、小錦しか攻撃できません。小錦の隣にいる敵は、小錦の隣から離れられません。小錦を敵の前線に寄せて足止めしましょう。");
        AddSection(scroll, "部の時間と中断", "第二局に勝ったあとから、局の合間に「部の時間」があり、2つのできごとから1つを選べます。対局中は上の「中断」でタイトルに戻れ、タイトルの「続きから」でその局の初めから再開できます。");
        AddSection(scroll, "手数の上限", "1局が" + GameManager.MoveLimit + "手を超えると、残った駒の強さとC3の体力で判定になります。");

        var close = UIFactory.Button("閉じる", () => { helpOverlay.RemoveFromHierarchy(); helpOverlay = null; });
        close.style.alignSelf = Align.Center;
        close.style.marginTop = 12;
        panel.Add(close);
    }

    /// <summary>部員ごとの「能力が決まる（活躍）」の一覧。敵を倒すのはどの部員も活躍</summary>
    private static string FeatList()
    {
        var members = new[]
        {
            new System.Collections.Generic.KeyValuePair<string, PieceType>("門人", PieceType.Monin),
            new System.Collections.Generic.KeyValuePair<string, PieceType>("僕", PieceType.Boku),
            new System.Collections.Generic.KeyValuePair<string, PieceType>("ヲツ", PieceType.Wotsu),
            new System.Collections.Generic.KeyValuePair<string, PieceType>("なこ", PieceType.Nako),
            new System.Collections.Generic.KeyValuePair<string, PieceType>("李白", PieceType.Rihaku),
            new System.Collections.Generic.KeyValuePair<string, PieceType>("小錦", PieceType.Konishiki),
            new System.Collections.Generic.KeyValuePair<string, PieceType>("けい", PieceType.Kei),
            new System.Collections.Generic.KeyValuePair<string, PieceType>("きぷ", PieceType.Kipu),
            new System.Collections.Generic.KeyValuePair<string, PieceType>("ユウ", PieceType.Yuu),
            new System.Collections.Generic.KeyValuePair<string, PieceType>("物鉄", PieceType.Monotetsu),
            new System.Collections.Generic.KeyValuePair<string, PieceType>("光晴", PieceType.Mitsuharu),
            new System.Collections.Generic.KeyValuePair<string, PieceType>("ニコ", PieceType.Niko),
            new System.Collections.Generic.KeyValuePair<string, PieceType>("〆鯖", PieceType.Shimesaba),
            new System.Collections.Generic.KeyValuePair<string, PieceType>("翡翠", PieceType.Kawasemi),
        };
        var sb = new System.Text.StringBuilder("敵を倒すのは、どの部員でも活躍です。それに加えて:");
        foreach (var m in members) sb.Append("\n・" + m.Key + ": " + RunRoster.FeatText(m.Value));
        sb.Append("\n・SN: 敵を倒したときだけ（覚醒はしない）");
        sb.Append("\n活躍で練度がたまるのは1局に" + BalanceTuning.FeatXpMaxPerStage + "回までですが、覚醒に向けた活躍は何回でも数えます。");
        return sb.ToString();
    }

    private static void AddSection(VisualElement parent, string heading, string body)
    {
        var h = UIFactory.Label(heading, 21, Palette.GoldLight, "c3-bold");
        h.style.marginTop = 10;
        parent.Add(h);
        var b = UIFactory.Paragraph(body, 19, Palette.Text);
        b.style.marginTop = 4;
        b.style.marginBottom = 8;
        parent.Add(b);
    }
}
