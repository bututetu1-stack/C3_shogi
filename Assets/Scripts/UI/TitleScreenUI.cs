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
        if (helpOverlay != null) helpOverlay.RemoveFromHierarchy();
        helpOverlay = HelpUI.Show(uiDocument.rootVisualElement, () => helpOverlay = null);
    }
}
