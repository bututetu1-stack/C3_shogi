using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>タイトル画面（はじめる・遊び方・音量）</summary>
public class TitleScreenUI : MonoBehaviour
{
    public const string VolumeKey = "c3_volume";

    private static TitleScreenUI instance;
    private UIDocument uiDocument;
    private Action onStart;
    private VisualElement helpOverlay;

    public static void Show(Action onStart)
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

        var start = UIFactory.Button("対局を始める", OnStartClicked, "c3-button--primary", "c3-button--big");
        start.style.width = 320;
        start.style.marginBottom = 14;
        column.Add(start);

        var help = UIFactory.Button("遊び方", ShowHelp);
        help.style.width = 320;
        help.style.marginBottom = 26;
        column.Add(help);

        // 音量
        var volumeRow = new VisualElement();
        volumeRow.style.flexDirection = FlexDirection.Row;
        volumeRow.style.alignItems = Align.Center;
        volumeRow.Add(UIFactory.Label("音量", 16, Palette.TextSub, "c3-bold"));
        var slider = new Slider(0f, 1f);
        slider.value = AudioListener.volume;
        slider.style.width = 240;
        slider.style.marginLeft = 12;
        slider.RegisterValueChangedCallback(evt =>
        {
            AudioListener.volume = evt.newValue;
            PlayerPrefs.SetFloat(VolumeKey, evt.newValue);
        });
        volumeRow.Add(slider);
        column.Add(volumeRow);

        var credit = UIFactory.Label("フォント: しっぽり明朝 B1 / Zen角ゴシック New（SIL Open Font License）", 12, new Color(1f, 1f, 1f, 0.35f));
        credit.style.position = Position.Absolute;
        credit.style.bottom = 14;
        credit.style.right = 18;
        root.Add(credit);
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
        AddSection(scroll, "仲間と強化", "各局の前に、新しい仲間か全軍の強化をひとつ選びます。仲間は次の局にも連れて行けます。選び直しは1局につき1回まで。");
        AddSection(scroll, "手数の上限", "1局が" + GameManager.MoveLimit + "手を超えると、残った駒の強さとC3の体力で判定になります。");

        var close = UIFactory.Button("閉じる", () => { helpOverlay.RemoveFromHierarchy(); helpOverlay = null; });
        close.style.alignSelf = Align.Center;
        close.style.marginTop = 12;
        panel.Add(close);
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
