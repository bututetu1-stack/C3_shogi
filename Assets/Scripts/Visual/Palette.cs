using UnityEngine;

/// <summary>ゲーム全体で使う色（漆黒×金×木目の和風パレット）</summary>
public static class Palette
{
    // 背景
    public static readonly Color Background = Hex(0x17130F);
    public static readonly Color BackgroundGlow = Hex(0x3A2A1C);

    // 盤
    public static readonly Color BoardWood = Hex(0xE2B86E);
    public static readonly Color BoardWoodDark = Hex(0xC99A50);
    public static readonly Color BoardLine = new Color(0.22f, 0.14f, 0.07f, 0.85f);
    public static readonly Color BoardFrame = Hex(0x5A3A1F);
    public static readonly Color BoardFrameDark = Hex(0x2E1D10);
    public static readonly Color Gold = Hex(0xD4AF5F);
    public static readonly Color GoldLight = Hex(0xF2D58E);
    public static readonly Color CoordText = new Color(0.86f, 0.74f, 0.5f, 0.85f);

    // 陣営（味方=藍、敵=朱）
    public static readonly Color Player = Hex(0x4C7FD0);
    public static readonly Color PlayerLight = Hex(0x8DB4F0);
    public static readonly Color Enemy = Hex(0xD24B3A);
    public static readonly Color EnemyLight = Hex(0xF2856F);
    public static readonly Color PlayerZoneTint = new Color(0.30f, 0.50f, 0.85f, 0.10f);
    public static readonly Color EnemyZoneTint = new Color(0.85f, 0.30f, 0.22f, 0.10f);

    // ハイライト
    public static readonly Color MoveDot = new Color(0.08f, 0.42f, 0.24f, 0.85f);
    public static readonly Color DangerDot = new Color(0.80f, 0.30f, 0.12f, 0.85f);
    public static readonly Color AttackRing = new Color(0.90f, 0.20f, 0.15f, 0.95f);
    public static readonly Color SelectedFrame = new Color(1f, 0.85f, 0.35f, 1f);
    public static readonly Color LastMoveTint = new Color(1f, 0.92f, 0.45f, 0.28f);
    public static readonly Color HoverTint = new Color(1f, 1f, 1f, 0.12f);

    // 駒
    public static readonly Color Ink = Hex(0x1A120C);
    public static readonly Color PromotedInk = Hex(0xB3261E);
    public static readonly Color PieceShadow = new Color(0f, 0f, 0f, 0.38f);

    // ステータス
    public static readonly Color ATK = Hex(0xD9503F);
    public static readonly Color DEF = Hex(0x4F7FCF);
    public static readonly Color HP = Hex(0x4FA868);

    // UI
    public static readonly Color PanelBg = new Color(0.08f, 0.065f, 0.055f, 0.92f);
    public static readonly Color PanelBorder = new Color(0.83f, 0.69f, 0.37f, 0.55f);
    public static readonly Color Text = Hex(0xF3EBDD);
    public static readonly Color TextSub = Hex(0xBFB3A0);

    public static Color TeamColor(Team team) { return team == Team.Player ? Player : Enemy; }
    public static Color TeamLight(Team team) { return team == Team.Player ? PlayerLight : EnemyLight; }

    /// <summary>レアリティごとの駒の地色</summary>
    public static Color RarityBody(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Bronze: return Hex(0xC98A56);     // 銅
            case Rarity.Normal: return Hex(0xF1D7A0);     // 黄楊
            case Rarity.Rare: return Hex(0xD3DAE2);       // 銀
            case Rarity.SuperRare: return Hex(0xE6C25A);  // 金
            case Rarity.Legend: return Hex(0x4B2A6B);     // 紫漆
            default: return Color.white;
        }
    }

    /// <summary>レアリティの文字色（UIのラベル用）</summary>
    public static Color RarityLabel(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Bronze: return Hex(0xD8975F);
            case Rarity.Normal: return Hex(0xD9CDB8);
            case Rarity.Rare: return Hex(0xA9C4E8);
            case Rarity.SuperRare: return Hex(0xF0C949);
            case Rarity.Legend: return Hex(0xC79AF0);
            default: return Color.white;
        }
    }

    public static string RarityName(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Bronze: return "ブロンズ";
            case Rarity.Normal: return "ノーマル";
            case Rarity.Rare: return "レア";
            case Rarity.SuperRare: return "激レア";
            case Rarity.Legend: return "レジェンド";
            default: return "";
        }
    }

    /// <summary>駒の上の文字色（紫漆の駒だけ金文字）</summary>
    public static Color PieceInk(Rarity rarity, bool promoted)
    {
        if (promoted) return rarity == Rarity.Legend ? EnemyLight : PromotedInk;
        return rarity == Rarity.Legend ? GoldLight : Ink;
    }

    public static string ToHex(Color c)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    public static Color Hex(int rgb)
    {
        return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
