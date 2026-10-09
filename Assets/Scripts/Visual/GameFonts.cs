using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UIElements;
using TMPro;

/// <summary>
/// フォントの読み込みと共有。
/// 駒の文字 = しっぽり明朝B1 ExtraBold、UI・数字 = Zen角ゴシック New（Bold/Medium）
/// </summary>
public static class GameFonts
{
    private const string MinchoPath = "Fonts/ShipporiMinchoB1-ExtraBold";
    private const string GothicBoldPath = "Fonts/ZenKakuGothicNew-Bold";
    private const string GothicMediumPath = "Fonts/ZenKakuGothicNew-Medium";

    private static Font mincho;
    private static Font gothicBold;
    private static Font gothicMedium;
    private static TMP_FontAsset pieceTMP;
    private static TMP_FontAsset numberTMP;
    private static Material numberOutlineMaterial;
    private static Material labelMaterial;

    public static Font Mincho { get { return mincho != null ? mincho : (mincho = Resources.Load<Font>(MinchoPath)); } }
    public static Font GothicBold { get { return gothicBold != null ? gothicBold : (gothicBold = Resources.Load<Font>(GothicBoldPath)); } }
    public static Font GothicMedium { get { return gothicMedium != null ? gothicMedium : (gothicMedium = Resources.Load<Font>(GothicMediumPath)); } }

    // ---------------- UI Toolkit ----------------
    public static FontDefinition UIRegular { get { return FontDefinition.FromFont(GothicMedium); } }
    public static FontDefinition UIBold { get { return FontDefinition.FromFont(GothicBold); } }
    public static FontDefinition UIMincho { get { return FontDefinition.FromFont(Mincho); } }

    // ---------------- TextMeshPro（盤上の駒） ----------------
    /// <summary>駒の文字用（明朝）</summary>
    public static TMP_FontAsset PieceTMP
    {
        get
        {
            if (pieceTMP == null)
            {
                pieceTMP = CreateDynamic(Mincho, "PieceMincho");
                var fallback = NumberTMP;
                if (pieceTMP != null && fallback != null)
                    pieceTMP.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { fallback };
            }
            return pieceTMP;
        }
    }

    /// <summary>ステータスの数字・ダメージ表示用（ゴシック）</summary>
    public static TMP_FontAsset NumberTMP
    {
        get
        {
            if (numberTMP == null) numberTMP = CreateDynamic(GothicBold, "NumberGothic");
            return numberTMP;
        }
    }

    /// <summary>駒の文字用の共有マテリアル</summary>
    public static Material PieceLabelMaterial
    {
        get
        {
            if (labelMaterial == null && PieceTMP != null)
            {
                labelMaterial = new Material(PieceTMP.material);
                labelMaterial.name = "PieceLabel (shared)";
            }
            return labelMaterial;
        }
    }

    /// <summary>縁取り付き数字の共有マテリアル</summary>
    public static Material NumberOutlineMaterial
    {
        get
        {
            if (numberOutlineMaterial == null && NumberTMP != null)
            {
                numberOutlineMaterial = new Material(NumberTMP.material);
                numberOutlineMaterial.name = "NumberOutline (shared)";
                numberOutlineMaterial.EnableKeyword("OUTLINE_ON");
                numberOutlineMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
                numberOutlineMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.9f));
                numberOutlineMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.15f);
            }
            return numberOutlineMaterial;
        }
    }

    private static TMP_FontAsset CreateDynamic(Font font, string name)
    {
        if (font == null)
        {
            Debug.LogWarning("フォントが見つかりません: " + name);
            return null;
        }
        var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, true);
        if (asset != null) asset.name = name;
        return asset;
    }

    /// <summary>UIDocumentのルートにゲーム用フォントを設定する（子要素に継承される）</summary>
    public static void ApplyUIFont(VisualElement root)
    {
        if (root == null || GothicMedium == null) return;
        root.style.unityFontDefinition = UIRegular;
    }
}
