using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>局の合間のできごと（2枚のカードから1つ選ぶ）</summary>
public class StageEventUI : MonoBehaviour
{
    private static StageEventUI instance;
    private UIDocument uiDocument;
    private Action<StageEventKind> onChosen;
    private StageEventKind? selected;
    private readonly List<VisualElement> cards = new List<VisualElement>();
    private VisualElement detail;
    private Button confirm;

    public static void Show(int stage, List<StageEventKind> events, Action<StageEventKind> chosen)
    {
        Ensure().Build(stage, events, chosen);
    }

    private static StageEventUI Ensure()
    {
        if (instance != null) return instance;
        var obj = new GameObject("StageEventUI");
        instance = obj.AddComponent<StageEventUI>();
        instance.uiDocument = obj.AddComponent<UIDocument>();
        foreach (var d in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            if (d != instance.uiDocument && d.panelSettings != null)
            {
                instance.uiDocument.panelSettings = d.panelSettings;
                break;
            }
        }
        instance.uiDocument.sortingOrder = 80;
        return instance;
    }

    private void Build(int stage, List<StageEventKind> events, Action<StageEventKind> chosen)
    {
        onChosen = chosen;
        selected = null;
        cards.Clear();
        VisualElement root = UIFactory.SetupRoot(uiDocument);
        root.Clear();
        root.pickingMode = PickingMode.Position;

        var overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = overlay.style.right = overlay.style.top = overlay.style.bottom = 0;
        overlay.style.backgroundColor = new Color(0.03f, 0.025f, 0.02f, 0.88f);
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        root.Add(overlay);

        var sub = UIFactory.Label("第" + UIFactory.Kanji(stage) + "局の前に", 22, Palette.Gold, "c3-mincho");
        overlay.Add(sub);
        var title = UIFactory.Label("部の時間", 40, Palette.Text, "c3-mincho");
        title.style.letterSpacing = 8;
        title.style.marginBottom = 18;
        overlay.Add(title);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.Center;
        foreach (var kind in events) row.Add(Card(kind));
        overlay.Add(row);

        detail = UIFactory.Panel();
        detail.style.width = 760;
        detail.style.minHeight = 100;
        detail.style.marginTop = 14;
        detail.style.paddingLeft = detail.style.paddingRight = 28;
        detail.Add(UIFactory.Label("どちらかひとつ選んでください", 20, Palette.TextSub));
        overlay.Add(detail);

        confirm = UIFactory.Button("これにする", OnConfirm, "c3-button--primary", "c3-button--big");
        confirm.style.marginTop = 14;
        confirm.SetEnabled(false);
        overlay.Add(confirm);
    }

    private VisualElement Card(StageEventKind kind)
    {
        var card = new VisualElement();
        card.AddToClassList("c3-card");

        var icon = new VisualElement();
        icon.style.width = icon.style.height = 96;
        icon.style.alignItems = Align.Center;
        icon.style.justifyContent = Justify.Center;
        icon.style.borderTopLeftRadius = icon.style.borderTopRightRadius = icon.style.borderBottomLeftRadius = icon.style.borderBottomRightRadius = 48;
        Color c = StageEvents.GlyphColor(kind);
        icon.style.backgroundColor = new Color(c.r, c.g, c.b, 0.22f);
        icon.style.borderTopWidth = icon.style.borderBottomWidth = icon.style.borderLeftWidth = icon.style.borderRightWidth = 3;
        icon.style.borderTopColor = icon.style.borderBottomColor = icon.style.borderLeftColor = icon.style.borderRightColor = c;
        icon.pickingMode = PickingMode.Ignore;
        icon.Add(UIFactory.Label(StageEvents.Glyph(kind), 50, Color.Lerp(c, Color.white, 0.4f), "c3-mincho"));
        card.Add(icon);

        var name = UIFactory.Label(StageEvents.Title(kind), 28, Palette.Text, "c3-mincho");
        name.style.marginTop = 10;
        card.Add(name);

        card.RegisterCallback<ClickEvent>(evt => Select(kind, card));
        cards.Add(card);
        return card;
    }

    private void Select(StageEventKind kind, VisualElement card)
    {
        selected = kind;
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayClick();
        foreach (var c in cards) c.EnableInClassList("c3-card--selected", c == card);
        detail.Clear();
        detail.Add(UIFactory.Label(StageEvents.Title(kind), 28, Palette.Text, "c3-mincho"));
        var body = UIFactory.Paragraph(StageEvents.Description(kind), 20, Palette.Text);
        body.style.marginTop = 6;
        detail.Add(body);
        confirm.SetEnabled(true);
    }

    private void OnConfirm()
    {
        if (!selected.HasValue) return;
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            uiDocument.rootVisualElement.Clear();
            uiDocument.rootVisualElement.pickingMode = PickingMode.Ignore;
        }
        Action<StageEventKind> cb = onChosen;
        onChosen = null;
        if (cb != null) cb(selected.Value);
    }
}
