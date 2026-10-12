using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>UI Toolkitの要素を共通のテーマで組み立てるためのヘルパー</summary>
public static class UIFactory
{
    private static StyleSheet theme;

    private static readonly string[] KanjiDigits = { "〇", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

    public static StyleSheet Theme
    {
        get
        {
            if (theme == null) theme = Resources.Load<StyleSheet>("UI/Theme");
            return theme;
        }
    }

    /// <summary>UIDocumentのルートを画面全体に広げ、テーマとフォントを設定する</summary>
    public static VisualElement SetupRoot(UIDocument doc)
    {
        VisualElement root = doc.rootVisualElement;
        root.Clear();
        if (Theme != null && !root.styleSheets.Contains(Theme)) root.styleSheets.Add(Theme);
        root.AddToClassList("c3-root");
        GameFonts.ApplyUIFont(root);
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.pickingMode = PickingMode.Ignore;
        return root;
    }

    /// <summary>
    /// 縦スクロールを作る。スクロールバーをつかんだあとにバーへフォーカスが残ると、
    /// ゲームパッドやジョイスティックのわずかな傾き（「上」の入力）でゆっくり動き続けるので、
    /// バーにはフォーカスを渡さず、上下左右の入力もスクロールに届かないようにする。
    /// </summary>
    public static ScrollView Scroll()
    {
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.focusable = false;
        scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
        foreach (var s in new[] { scroll.verticalScroller, scroll.horizontalScroller })
        {
            s.focusable = false;
            s.slider.focusable = false;
        }
        scroll.RegisterCallback<NavigationMoveEvent>(e => e.StopPropagation(), TrickleDown.TrickleDown);
        return scroll;
    }

    public static Label Label(string text, float fontSize, Color color, params string[] classes)
    {
        var label = new Label(text);
        label.style.fontSize = fontSize;
        label.style.color = color;
        foreach (var c in classes) label.AddToClassList(c);
        label.pickingMode = PickingMode.Ignore;
        return label;
    }

    public static Label Label(string text, float fontSize, params string[] classes)
    {
        return Label(text, fontSize, Palette.Text, classes);
    }

    /// <summary>親の幅いっぱいに広げて、文字を真ん中にそろえた1行（駒の絵の文字や、駒の下の★など）</summary>
    public static Label CenteredLabel(string text, float fontSize, Color color, params string[] classes)
    {
        var label = Label(text, fontSize, color, classes);
        label.style.alignSelf = Align.Stretch;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.marginLeft = label.style.marginRight = 0;
        label.style.paddingLeft = label.style.paddingRight = 0;
        return label;
    }

    /// <summary>複数行の文章（日本語の改行ルールで自動改行）</summary>
    public static WrappedLabel Paragraph(string text, float fontSize, Color color, params string[] classes)
    {
        var label = new WrappedLabel(text);
        label.style.fontSize = fontSize;
        label.style.color = color;
        foreach (var c in classes) label.AddToClassList(c);
        return label;
    }

    /// <summary>キャラの説明（人となり）: 薄い色の文字に、左の細い線</summary>
    public static VisualElement FlavorBox(string text, float fontSize)
    {
        var box = new VisualElement();
        box.style.borderLeftWidth = 3;
        box.style.borderLeftColor = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.45f);
        box.style.paddingLeft = 12;
        box.style.marginTop = 12;
        box.Add(Paragraph(text, fontSize, Palette.TextSub));
        return box;
    }

    /// <summary>能力の説明: 見出しと、うっすら色の付いた枠</summary>
    public static VisualElement AbilityBox(string heading, string text, float fontSize, Color headingColor)
    {
        var box = new VisualElement();
        box.style.marginTop = 10;
        box.style.paddingLeft = box.style.paddingRight = 12;
        box.style.paddingTop = 8;
        box.style.paddingBottom = 10;
        box.style.backgroundColor = new Color(headingColor.r, headingColor.g, headingColor.b, 0.08f);
        Color edge = new Color(headingColor.r, headingColor.g, headingColor.b, 0.4f);
        box.style.borderTopWidth = box.style.borderBottomWidth = box.style.borderLeftWidth = box.style.borderRightWidth = 1;
        box.style.borderTopColor = box.style.borderBottomColor = box.style.borderLeftColor = box.style.borderRightColor = edge;
        box.style.borderTopLeftRadius = box.style.borderTopRightRadius = box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 8;
        var head = Label(heading, fontSize - 2, headingColor, "c3-bold");
        head.style.marginBottom = 4;
        box.Add(head);
        box.Add(Paragraph(text, fontSize, Palette.Text));
        return box;
    }

    /// <summary>駒の説明をキャラと能力の2つの枠に分けて並べる（空のほうは出さない）</summary>
    public static VisualElement PieceText(string flavor, string ability, float fontSize)
    {
        var box = new VisualElement();
        if (!string.IsNullOrEmpty(flavor)) box.Add(FlavorBox(flavor, fontSize - 1));
        if (!string.IsNullOrEmpty(ability)) box.Add(AbilityBox("能力", ability, fontSize, Palette.GoldLight));
        return box;
    }

    /// <summary>その姿のキャラの説明（成った姿の説明がなければ、成る前のもの）</summary>
    public static string FlavorOf(PieceData d, bool promoted)
    {
        if (promoted && !string.IsNullOrEmpty(d.promotedFlavor)) return d.promotedFlavor;
        return d.flavor;
    }

    /// <summary>
    /// 練度の★を1段ずつ並べる（★・効果・必要な練度・あといくつ）。
    /// afterXp が0以上なら、その練度で新しく届く★を「鍛えると届く」と目立たせる。
    /// </summary>
    public static VisualElement StarLadder(Rarity rarity, int xp, float fontSize, int afterXp = -1)
    {
        var box = new VisualElement();
        box.style.maxWidth = fontSize * 28;
        int[] t = RunRoster.Thresholds(rarity);
        for (int i = 0; i < t.Length; i++)
        {
            int star = i + 1;
            bool done = xp >= t[i];
            bool soon = !done && afterXp >= t[i];
            Color c = done ? Palette.GoldLight : soon ? Palette.HP : Palette.TextSub;
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 2;
            var s = Label((done ? "★" : "☆") + star, fontSize, c, "c3-bold");
            s.style.width = fontSize * 2.4f;
            row.Add(s);
            var eff = Label(RunRoster.StarEffectText(star), fontSize, done || soon ? Palette.Text : Palette.TextSub);
            eff.style.flexGrow = 1;
            row.Add(eff);
            string state = done ? "達成" : soon ? "鍛えると届く" : "あと" + (t[i] - xp);
            row.Add(soon ? Label("練度" + t[i] + "（" + state + "）", fontSize - 2, c, "c3-bold") : Label("練度" + t[i] + "（" + state + "）", fontSize - 2, c));
            box.Add(row);
        }
        return box;
    }

    /// <summary>1行に1つずつの箇条書き（行頭は「・」）</summary>
    public static VisualElement Bullets(System.Collections.Generic.IEnumerable<string> lines, float fontSize, Color color)
    {
        var box = new VisualElement();
        foreach (var line in lines)
        {
            var p = Paragraph("・" + line, fontSize, color);
            p.style.marginTop = 2;
            box.Add(p);
        }
        return box;
    }

    /// <summary>動きの図の凡例</summary>
    public static Label MoveLegend()
    {
        var legend = Label("●1マス　▲どこまでも　○飛び越え", 14, Palette.TextSub);
        legend.style.marginTop = 8;
        legend.style.unityTextAlign = TextAnchor.MiddleCenter;
        return legend;
    }

    public static Button Button(string text, Action onClick, params string[] classes)
    {
        var button = new Button(() =>
        {
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlayClick();
            if (onClick != null) onClick();
        });
        button.text = text;
        button.AddToClassList("c3-button");
        foreach (var c in classes) button.AddToClassList(c);
        return button;
    }

    public static VisualElement Panel(params string[] classes)
    {
        var panel = new VisualElement();
        panel.AddToClassList("c3-panel");
        foreach (var c in classes) panel.AddToClassList(c);
        return panel;
    }

    public static Label PanelTitle(string text)
    {
        var label = new Label(text);
        label.AddToClassList("c3-panel-title");
        label.pickingMode = PickingMode.Ignore;
        return label;
    }

    public static VisualElement Separator()
    {
        var sep = new VisualElement();
        sep.AddToClassList("c3-separator");
        sep.pickingMode = PickingMode.Ignore;
        return sep;
    }

    public static Label Chip(string text, Color color)
    {
        var chip = new Label(text);
        chip.AddToClassList("c3-chip");
        chip.style.flexShrink = 0;
        chip.style.marginBottom = 3;
        chip.style.color = color;
        chip.style.borderTopColor = chip.style.borderBottomColor = chip.style.borderLeftColor = chip.style.borderRightColor = new Color(color.r, color.g, color.b, 0.6f);
        chip.style.backgroundColor = new Color(color.r, color.g, color.b, 0.12f);
        chip.pickingMode = PickingMode.Ignore;
        return chip;
    }

    /// <summary>攻撃・防御・体力の3つの枠</summary>
    public static VisualElement StatRow(int atk, int def, string hp)
    {
        var row = new VisualElement();
        row.AddToClassList("c3-stat-row");
        row.Add(Stat("攻撃", atk.ToString(), Palette.ATK));
        row.Add(Stat("防御", def.ToString(), Palette.DEF));
        row.Add(Stat("体力", hp, Palette.HP));
        return row;
    }

    private static VisualElement Stat(string label, string value, Color color)
    {
        var box = new VisualElement();
        box.AddToClassList("c3-stat");
        box.style.borderTopColor = color;
        box.Add(Label(label, 15, "c3-stat__label"));
        var v = Label(value, 26, Color.Lerp(color, Color.white, 0.55f), "c3-stat__value");
        box.Add(v);
        box.pickingMode = PickingMode.Ignore;
        return box;
    }

    public static string FormatHP(int hp)
    {
        return hp >= 999 ? "∞" : hp.ToString();
    }

    /// <summary>駒のアイコン（立ち絵があればそれ、なければ五角形の駒）</summary>
    public static VisualElement PieceIcon(PieceData data, bool promoted, float size)
    {
        var icon = new VisualElement();
        icon.style.width = size;
        icon.style.height = size;
        icon.style.alignItems = Align.Center;
        icon.style.justifyContent = Justify.Center;
        icon.style.flexShrink = 0;
        icon.pickingMode = PickingMode.Ignore;

        Sprite portrait = promoted && data.canPromote && data.promotedPortrait != null ? data.promotedPortrait : data.portrait;
        if (portrait != null)
        {
            icon.style.backgroundImage = new StyleBackground(portrait);
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            return icon;
        }

        PieceLook look = PieceSkin.For(data, promoted);
        icon.style.backgroundImage = new StyleBackground(look.body.texture);
        icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);

        string name = promoted && data.canPromote ? data.promotedDisplayName : data.displayName;
        if (string.IsNullOrEmpty(name)) name = "?";
        Color ink = look.ink;

        if (look.emblem != null)
        {
            var emblem = new VisualElement();
            emblem.style.position = Position.Absolute;
            emblem.style.width = size * 0.17f;
            emblem.style.height = size * 0.17f;
            emblem.style.top = size * 0.06f;
            emblem.style.left = size * 0.415f;
            emblem.style.backgroundImage = new StyleBackground(look.emblem);
            emblem.style.unityBackgroundImageTintColor = look.emblemColor;
            emblem.pickingMode = PickingMode.Ignore;
            icon.Add(emblem);
        }

        // 文字の箱は駒の幅いっぱいにして、その中で真ん中にそろえる（文字の幅の箱を真ん中に置くと、少し左に寄って見えた）
        var labels = new VisualElement();
        labels.style.alignSelf = Align.Stretch;
        labels.style.marginTop = size * 0.1f;
        labels.pickingMode = PickingMode.Ignore;
        bool vertical = name.Length == 2 && !IsAscii(name);
        if (vertical)
        {
            foreach (char ch in name)
            {
                var l = CenteredLabel(ch.ToString(), size * 0.27f, ink, "c3-mincho");
                l.style.marginTop = -size * 0.04f;
                l.style.marginBottom = -size * 0.04f;
                labels.Add(l);
            }
        }
        else
        {
            labels.Add(CenteredLabel(name, size * (name.Length == 1 ? 0.42f : 0.26f), ink, "c3-mincho"));
        }
        icon.Add(labels);
        return icon;
    }

    /// <summary>
    /// 駒の動きの図。● 1マス、▲ どこまでも（端の矢印）、○ 飛び越え
    /// </summary>
    public static VisualElement MoveGrid(MoveDirection[] dirs, bool flipY, float cellSize, int range = 4)
    {
        int n = range * 2 + 1;
        var steps = new HashSet<Vector2Int>();
        var slides = new HashSet<Vector2Int>();
        var jumps = new HashSet<Vector2Int>();
        var arrows = new Dictionary<Vector2Int, Vector2Int>();

        if (dirs != null)
        {
            foreach (var dir in dirs)
            {
                Vector2Int step = flipY ? new Vector2Int(dir.direction.x, -dir.direction.y) : dir.direction;
                if (dir.canJump)
                {
                    Vector2Int p = step * Mathf.Min(dir.maxDistance, range);
                    if (Mathf.Abs(p.x) <= range && Mathf.Abs(p.y) <= range) jumps.Add(p);
                    continue;
                }
                bool slide = dir.maxDistance > range;
                int show = Mathf.Min(dir.maxDistance, range);
                for (int d = 1; d <= show; d++)
                {
                    Vector2Int p = step * d;
                    if (Mathf.Abs(p.x) > range || Mathf.Abs(p.y) > range) break;
                    if (slide) slides.Add(p); else steps.Add(p);
                    if (slide && d == show) arrows[p] = step;
                }
            }
        }

        var grid = new VisualElement();
        grid.AddToClassList("c3-move-grid");
        grid.pickingMode = PickingMode.Ignore;
        for (int y = range; y >= -range; y--)
        {
            var row = new VisualElement();
            row.AddToClassList("c3-move-row");
            row.pickingMode = PickingMode.Ignore;
            for (int x = -range; x <= range; x++)
            {
                var cell = new VisualElement();
                cell.AddToClassList("c3-move-cell");
                cell.style.width = cellSize;
                cell.style.height = cellSize;
                cell.pickingMode = PickingMode.Ignore;
                var p = new Vector2Int(x, y);

                if (x == 0 && y == 0)
                {
                    cell.style.backgroundColor = Palette.BoardFrame;
                    cell.Add(Dot(cellSize * 0.5f, Palette.GoldLight, false));
                }
                else if (arrows.ContainsKey(p))
                {
                    var arrow = Label("▲", cellSize * 0.7f, Palette.PromotedInk, "c3-bold");
                    arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
                    Vector2Int d = arrows[p];
                    float angle = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
                    arrow.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
                    cell.Add(arrow);
                }
                else if (slides.Contains(p))
                {
                    cell.Add(Dot(cellSize * 0.42f, Palette.PromotedInk, false));
                }
                else if (steps.Contains(p))
                {
                    cell.Add(Dot(cellSize * 0.5f, Palette.Ink, false));
                }
                else if (jumps.Contains(p))
                {
                    cell.Add(Dot(cellSize * 0.5f, Palette.Ink, true));
                }
                row.Add(cell);
            }
            grid.Add(row);
        }
        return grid;
    }

    private static VisualElement Dot(float size, Color color, bool hollow)
    {
        var dot = new VisualElement();
        dot.style.width = size;
        dot.style.height = size;
        float r = size * 0.5f;
        dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius = dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = r;
        if (hollow)
        {
            float w = Mathf.Max(2f, size * 0.18f);
            dot.style.borderTopWidth = dot.style.borderBottomWidth = dot.style.borderLeftWidth = dot.style.borderRightWidth = w;
            dot.style.borderTopColor = dot.style.borderBottomColor = dot.style.borderLeftColor = dot.style.borderRightColor = color;
        }
        else
        {
            dot.style.backgroundColor = color;
        }
        dot.pickingMode = PickingMode.Ignore;
        return dot;
    }

    /// <summary>1〜99 を漢数字に（第三局・第十五局 など）</summary>
    public static string Kanji(int n)
    {
        if (n <= 0) return KanjiDigits[0];
        if (n < 10) return KanjiDigits[n];
        if (n < 100)
        {
            int tens = n / 10;
            int ones = n % 10;
            return (tens == 1 ? "" : KanjiDigits[tens]) + "十" + (ones == 0 ? "" : KanjiDigits[ones]);
        }
        return n.ToString();
    }

    /// <summary>駒の動き（成り後にデータがなければ通常の動き。動けない駒・成ると動けなくなる駒（提督）は空）</summary>
    public static MoveDirection[] MovesOf(PieceData data, bool promoted)
    {
        promoted = promoted && data.canPromote;
        if (data.isImmovable || (promoted && data.isImmovableWhenPromoted)) return new MoveDirection[0];
        if (promoted && data.promotedMoveDirections != null && data.promotedMoveDirections.Length > 0)
            return data.promotedMoveDirections;
        return data.moveDirections ?? new MoveDirection[0];
    }

    /// <summary>動きの図の下に添える「動けない」の注記（動ける駒なら null）</summary>
    public static Label ImmovableNote(MoveDirection[] moves)
    {
        if (moves != null && moves.Length > 0) return null;
        var note = Label("動けない", 18, Palette.TextSub, "c3-bold");
        note.style.marginTop = 6;
        return note;
    }

    private static bool IsAscii(string s)
    {
        foreach (char ch in s)
            if (ch > 0x7F) return false;
        return true;
    }
}
