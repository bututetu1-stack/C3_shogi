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
        box.Add(Label(label, 12, "c3-stat__label"));
        var v = Label(value, 22, Color.Lerp(color, Color.white, 0.55f), "c3-stat__value");
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

        Sprite portrait = data.portrait;
        if (portrait != null)
        {
            icon.style.backgroundImage = new StyleBackground(portrait);
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            return icon;
        }

        Rarity rarity = (promoted && data.hasPromotedRarity) ? data.promotedRarity : data.rarity;
        icon.style.backgroundImage = new StyleBackground(SpriteFactory.PieceTexture(rarity));
        icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);

        string name = promoted && data.canPromote ? data.promotedDisplayName : data.displayName;
        if (string.IsNullOrEmpty(name)) name = "?";
        Color ink = Palette.PieceInk(rarity, promoted && data.canPromote);

        var labels = new VisualElement();
        labels.style.alignItems = Align.Center;
        labels.style.marginTop = size * 0.1f;
        labels.pickingMode = PickingMode.Ignore;
        bool vertical = name.Length == 2 && !IsAscii(name);
        if (vertical)
        {
            foreach (char ch in name)
            {
                var l = Label(ch.ToString(), size * 0.27f, ink, "c3-mincho");
                l.style.marginTop = -size * 0.04f;
                l.style.marginBottom = -size * 0.04f;
                labels.Add(l);
            }
        }
        else
        {
            labels.Add(Label(name, size * (name.Length == 1 ? 0.42f : 0.26f), ink, "c3-mincho"));
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

    /// <summary>駒の動き（成り後にデータがなければ通常の動き）</summary>
    public static MoveDirection[] MovesOf(PieceData data, bool promoted)
    {
        if (promoted && data.canPromote && data.promotedMoveDirections != null && data.promotedMoveDirections.Length > 0)
            return data.promotedMoveDirections;
        return data.moveDirections;
    }

    private static bool IsAscii(string s)
    {
        foreach (char ch in s)
            if (ch > 0x7F) return false;
        return true;
    }
}
