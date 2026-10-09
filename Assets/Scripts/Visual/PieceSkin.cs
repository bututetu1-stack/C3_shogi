using UnityEngine;

/// <summary>駒の見た目（地の色・文字色・紋章など）</summary>
public struct PieceLook
{
    public Sprite body;
    public Color ink;
    public Sprite emblem;
    public Color emblemColor;
    public bool aura;       // 足元にほのかな光（提督）
    public bool floating;   // 海に浮かぶようにゆらゆら揺れる（艦娘・深海）
}

/// <summary>駒の種類・成りに応じた見た目を決める（物鉄・提督の艦隊は専用の見た目）</summary>
public static class PieceSkin
{
    public static readonly Color NavalInk = Palette.Hex(0x1E3358);
    public static readonly Color AbyssInk = Palette.Hex(0x86E8DC);

    public static PieceLook For(PieceData data, bool promoted)
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
                }
                else
                {
                    // 物鉄: 提督になる前から錨の刻印がうっすら入っている
                    look.emblem = SpriteFactory.Anchor;
                    look.emblemColor = new Color(look.ink.r, look.ink.g, look.ink.b, 0.5f);
                }
                break;

            case PieceType.Kanmusu:
                look.body = SpriteFactory.SpecialBody("steel");
                look.ink = NavalInk;
                look.emblem = SpriteFactory.Anchor;
                look.emblemColor = new Color(NavalInk.r, NavalInk.g, NavalInk.b, 0.8f);
                look.floating = true;
                break;

            case PieceType.Shinkai:
                look.body = SpriteFactory.SpecialBody("abyss");
                look.ink = AbyssInk;
                look.floating = true;
                break;
        }
        return look;
    }
}
