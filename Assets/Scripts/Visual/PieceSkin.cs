using UnityEngine;

/// <summary>駒の見た目（地の色・文字色・紋章など）</summary>
public struct PieceLook
{
    public Sprite body;
    public Color ink;
    public Sprite emblem;
    public Color emblemColor;
    public bool aura;       // 足元にほのかな光（提督・なこ・ボス）
    public Color auraColor;
    public bool auraRing;   // 光ではなく脈打つ輪（小錦の挑発）
    public bool floating;   // 海に浮かぶようにゆらゆら揺れる（艦娘・深海）
}

/// <summary>駒の種類・成りに応じた見た目を決める（物鉄・提督の艦隊は専用の見た目）</summary>
public static class PieceSkin
{
    public static readonly Color NavalInk = Palette.Hex(0x1E3358);
    public static readonly Color AbyssInk = Palette.Hex(0x86E8DC);

    /// <summary>kai2: 艦娘の改二（金の光をまとう）</summary>
    public static PieceLook For(PieceData data, bool promoted, bool kai2 = false)
    {
        promoted = promoted && data.canPromote;
        Rarity rarity = (promoted && data.hasPromotedRarity) ? data.promotedRarity : data.rarity;
        var look = new PieceLook
        {
            body = SpriteFactory.PieceBody(rarity),
            ink = Palette.PieceInk(rarity, promoted),
        };

        switch (data.pieceType)
        {
            case PieceType.Monotetsu:
                if (promoted)
                {
                    // 提督: 紺の漆に金文字と金の錨
                    look.body = SpriteFactory.SpecialBody("naval");
                    look.ink = Palette.GoldLight;
                    look.emblem = SpriteFactory.Anchor;
                    look.emblemColor = Palette.Gold;
                    look.aura = true;
                    look.auraColor = Palette.Gold;
                }
                else
                {
                    // 物鉄: 提督になる前から錨の刻印がうっすら入っている
                    look.emblem = SpriteFactory.Anchor;
                    look.emblemColor = new Color(look.ink.r, look.ink.g, look.ink.b, 0.5f);
                }
                break;



            case PieceType.Konishiki:
                // 挑発: 足元に赤い輪が脈打つ
                SetAura(ref look, Palette.ATK, true);
                break;

            case PieceType.Nako:
                // ドーパミンの桃色のきらめき
                SetAura(ref look, new Color(1f, 0.45f, 0.75f), false);
                break;

            case PieceType.Kei:
                // 異端: 足元に端末の緑の光
                if (promoted) SetAura(ref look, new Color(0.36f, 1f, 0.6f), false);
                break;

            case PieceType.Maou: SetAura(ref look, new Color(0.62f, 0.3f, 0.95f), false); break;
            case PieceType.Raitei: SetAura(ref look, new Color(1f, 0.88f, 0.35f), false); break;
            case PieceType.Ryuujin: SetAura(ref look, new Color(0.4f, 0.85f, 1f), false); break;
        }

        if (PieceTypes.IsKanmusu(data.pieceType))
        {
            // 艦娘: 鋼の駒に紺の文字と錨。改は赤文字、改二は金の光
            look.body = SpriteFactory.SpecialBody("steel");
            look.ink = promoted ? Palette.Hex(0xB0302A) : NavalInk;
            look.emblem = SpriteFactory.Anchor;
            look.emblemColor = new Color(NavalInk.r, NavalInk.g, NavalInk.b, 0.8f);
            look.floating = true;
            if (kai2) SetAura(ref look, Palette.Gold, false);
        }
        else if (PieceTypes.IsShinkai(data.pieceType))
        {
            // 深海: 深淵の駒。elite は赤、flagship は金、姫級は紫の光
            look.body = SpriteFactory.SpecialBody("abyss");
            look.ink = AbyssInk;
            look.floating = true;
            if (data.pieceType == PieceType.ShinkaiElite) SetAura(ref look, Palette.ATK, false);
            else if (data.pieceType == PieceType.ShinkaiFlagship) SetAura(ref look, Palette.Gold, false);
            else if (data.pieceType == PieceType.ShinkaiHime) SetAura(ref look, new Color(0.7f, 0.35f, 1f), false);
        }

        // 錨を画像に差し替えたときは、画像の色をそのまま使う（濃さだけ残す）
        if (look.emblem != null && EffectArt.Has("Anchor"))
            look.emblemColor = new Color(1f, 1f, 1f, look.emblemColor.a);
        return look;
    }

    private static void SetAura(ref PieceLook look, Color color, bool ring)
    {
        look.aura = true;
        look.auraColor = color;
        look.auraRing = ring;
    }
}
