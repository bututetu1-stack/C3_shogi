using UnityEngine;
using TMPro;

/// <summary>
/// 駒の見た目。構成:
///   root（論理位置）
///    ├ ShadowHolder … 落ち影（ずらして表示）
///    ├ Visual       … 本体・文字（敵駒は180度回転、揺れ・持ち上げはここに掛ける）
///    └ Stats        … 攻・防・体のバッジ（常に正立）
/// </summary>
public class PieceRenderer : MonoBehaviour
{
    public const int OrderShadow = 9;
    public const int OrderBody = 10;
    public const int OrderLabel = 11;
    public const int OrderBadge = 12;
    public const int OrderBadgeText = 13;

    private static readonly Vector3 ShadowOffset = new Vector3(0.035f, -0.055f, 0f);
    private static readonly Color StunnedTint = new Color(0.6f, 0.72f, 0.9f);

    private PieceInstance pieceInstance;
    private bool isEnemyPiece;
    private Rarity appliedRarity;
    private bool appliedPromoted;
    private bool appliedKai2;
    private string appliedName;

    private Transform visual;
    private Transform shadowHolder;
    private Transform stats;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer shadowRenderer;
    private SpriteRenderer emblemRenderer;
    private SpriteRenderer auraRenderer;
    private Color auraColor;
    private bool auraRing;
    private float auraPhase;
    private TextMeshPro labelTop;
    private TextMeshPro labelBottom;

    private Badge atkBadge;
    private Badge defBadge;
    private Badge hpBadge;

    private class Badge
    {
        public GameObject root;
        public SpriteRenderer icon;
        public TextMeshPro text;
    }

    public Transform Visual { get { return visual; } }
    public Transform ShadowHolder { get { return shadowHolder; } }
    public Transform Stats { get { return stats; } }
    public SpriteRenderer Body { get { return bodyRenderer; } }
    /// <summary>ふだんの本体の色（冷笑されて動けない駒は青白く沈ませる）</summary>
    public Color BodyRestColor { get { return pieceInstance != null && pieceInstance.stunned ? StunnedTint : Color.white; } }
    public float BaseScale { get; private set; }
    /// <summary>海に浮かぶ駒（艦娘・深海）はゆらゆら揺らす</summary>
    public bool IsFloating { get; private set; }

    public void Init(PieceInstance piece)
    {
        pieceInstance = piece;
        isEnemyPiece = piece.team == Team.Enemy;
        BaseScale = GetPieceScale(piece.data.pieceType);
        Quaternion facing = isEnemyPiece ? Quaternion.Euler(0, 0, 180) : Quaternion.identity;

        shadowHolder = CreateChild("ShadowHolder", transform);
        shadowHolder.localPosition = ShadowOffset;
        shadowHolder.localRotation = facing;
        shadowHolder.localScale = Vector3.one * BaseScale;
        shadowRenderer = shadowHolder.gameObject.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = SpriteFactory.PieceShadow;
        shadowRenderer.color = Palette.PieceShadow;
        shadowRenderer.sortingOrder = OrderShadow;

        visual = CreateChild("Visual", transform);
        visual.localRotation = facing;
        visual.localScale = Vector3.one * BaseScale;
        bodyRenderer = visual.gameObject.AddComponent<SpriteRenderer>();
        bodyRenderer.sortingOrder = OrderBody;

        labelTop = CreateLabel("LabelTop");
        labelBottom = CreateLabel("LabelBottom");

        // 紋章（錨）は尖った先のあたりに小さく
        var emblemObj = CreateChild("Emblem", visual);
        emblemObj.localPosition = new Vector3(0f, 0.41f, 0f);
        emblemObj.localScale = new Vector3(0.17f, 0.17f, 1f);
        emblemRenderer = emblemObj.gameObject.AddComponent<SpriteRenderer>();
        emblemRenderer.sortingOrder = OrderLabel;

        // 足元の光（提督）
        var auraObj = CreateChild("Aura", transform);
        auraObj.localScale = new Vector3(1.5f, 1.5f, 1f);
        auraRenderer = auraObj.gameObject.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = SpriteFactory.SoftCircle;
        auraRenderer.sortingOrder = OrderShadow - 1;
        auraRenderer.enabled = false;
        auraPhase = Random.value * 6.28f;

        // ステータスは駒から見て 左肩=攻撃、右肩=防御、右下=体力（数字は常に正立）
        stats = CreateChild("Stats", transform);
        float flip = isEnemyPiece ? -1f : 1f;
        atkBadge = CreateBadge("ATK", SpriteFactory.Circle, Palette.ATK, new Vector3(-0.34f, 0.34f, 0f) * flip, 0.27f);
        defBadge = CreateBadge("DEF", SpriteFactory.Shield, Palette.DEF, new Vector3(0.34f, 0.34f, 0f) * flip, 0.27f);
        hpBadge = CreateBadge("HP", SpriteFactory.Circle, Palette.HP, new Vector3(0.34f, -0.36f, 0f) * flip, 0.27f);

        appliedName = null;
        RefreshBody(true);
        UpdateAllStats();
    }

    /// <summary>駒の種類ごとの大きさ（本物の将棋駒のように格で大きさを変える）</summary>
    private static float GetPieceScale(PieceType type)
    {
        switch (type)
        {
            case PieceType.C3: return 0.94f;
            case PieceType.Pawn: return 0.80f;
            case PieceType.Chuka:
            case PieceType.Dopa: return 0.72f;
            case PieceType.Maou:
            case PieceType.ShinkaiHime:
            case PieceType.Ryuujin:
            case PieceType.Raitei: return 0.92f;
            default: return 0.86f;
        }
    }

    private static Transform CreateChild(string name, Transform parent)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj.transform;
    }

    private TextMeshPro CreateLabel(string name)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(visual, false);
        var tmp = obj.AddComponent<TextMeshPro>();
        tmp.font = GameFonts.PieceTMP;
        if (GameFonts.PieceLabelMaterial != null) tmp.fontSharedMaterial = GameFonts.PieceLabelMaterial;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.rectTransform.sizeDelta = new Vector2(1f, 0.5f);
        var mr = obj.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = OrderLabel;
        return tmp;
    }

    private Badge CreateBadge(string name, Sprite sprite, Color color, Vector3 pos, float size)
    {
        var badge = new Badge();
        badge.root = new GameObject(name);
        badge.root.transform.SetParent(stats, false);
        badge.root.transform.localPosition = pos;

        var iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(badge.root.transform, false);
        iconObj.transform.localScale = new Vector3(size, size, 1f);
        badge.icon = iconObj.AddComponent<SpriteRenderer>();
        badge.icon.sprite = sprite;
        badge.icon.color = color;
        badge.icon.sortingOrder = OrderBadge;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(badge.root.transform, false);
        textObj.transform.localPosition = new Vector3(0f, -0.005f, 0f);
        badge.text = textObj.AddComponent<TextMeshPro>();
        badge.text.font = GameFonts.NumberTMP;
        if (GameFonts.NumberOutlineMaterial != null) badge.text.fontSharedMaterial = GameFonts.NumberOutlineMaterial;
        badge.text.fontSize = 1.9f;
        badge.text.alignment = TextAlignmentOptions.Center;
        badge.text.textWrappingMode = TextWrappingModes.NoWrap;
        badge.text.color = Color.white;
        badge.text.rectTransform.sizeDelta = new Vector2(0.6f, 0.3f);
        var mr = textObj.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = OrderBadgeText;
        return badge;
    }

    // ------------------------------------------------------------
    // 更新
    // ------------------------------------------------------------

    public void UpdateHP()
    {
        if (pieceInstance == null) return;
        SetHPBadge();
    }

    public void UpdateAllStats()
    {
        if (pieceInstance == null) return;
        RefreshBody(false);
        bodyRenderer.color = BodyRestColor;

        SetBadge(atkBadge, pieceInstance.ATK);
        SetBadge(defBadge, pieceInstance.DEF);
        SetHPBadge();
    }

    private void SetBadge(Badge badge, int value)
    {
        badge.root.SetActive(value > 0);
        badge.text.text = value.ToString();
    }

    private void SetHPBadge()
    {
        int hp = pieceInstance.currentHP;
        string text = hp >= 999 ? "∞" : hp.ToString();
        hpBadge.text.text = text;
        hpBadge.text.fontSize = text.Length >= 2 ? 1.6f : 1.9f;
        hpBadge.root.SetActive(hp > 0);
    }

    /// <summary>本体の色・文字・紋章を状態（レアリティ・成り）に合わせる</summary>
    private void RefreshBody(bool force)
    {
        Rarity rarity = pieceInstance.CurrentRarity;
        bool promoted = pieceInstance.isPromoted;
        string name = pieceInstance.DisplayName ?? "";
        bool kai2 = pieceInstance.kai2;
        if (!force && rarity == appliedRarity && promoted == appliedPromoted && kai2 == appliedKai2 && name == appliedName) return;

        appliedRarity = rarity;
        appliedPromoted = promoted;
        appliedKai2 = kai2;
        appliedName = name;

        PieceLook look = PieceSkin.For(pieceInstance.data, promoted, kai2);
        bodyRenderer.sprite = look.body;
        labelTop.color = look.ink;
        labelBottom.color = look.ink;
        emblemRenderer.sprite = look.emblem;
        emblemRenderer.color = look.emblemColor;
        emblemRenderer.enabled = look.emblem != null;
        auraRenderer.enabled = look.aura;
        auraRenderer.sprite = look.auraRing ? SpriteFactory.Ring : SpriteFactory.SoftCircle;
        auraColor = look.auraColor;
        auraRing = look.auraRing;
        IsFloating = look.floating;

        // 紋章がある駒は文字を少し下げる
        float shift = look.emblem != null ? -0.04f : 0f;
        if (IsAscii(name) || name.Length == 1)
        {
            // 1文字または英数字（C3・SN）は1行で大きく
            labelTop.text = name;
            labelTop.fontSize = name.Length == 1 ? 4.6f : (name.Length == 2 ? 3.6f : 2.8f);
            labelTop.transform.localPosition = new Vector3(0f, -0.06f + shift, 0f);
            labelBottom.text = "";
        }
        else
        {
            // 2文字は縦書き
            labelTop.text = name.Substring(0, 1);
            labelBottom.text = name.Length > 1 ? name.Substring(1, 1) : "";
            labelTop.fontSize = 3.1f;
            labelBottom.fontSize = 3.1f;
            labelTop.transform.localPosition = new Vector3(0f, 0.16f + shift, 0f);
            labelBottom.transform.localPosition = new Vector3(0f, -0.19f + shift, 0f);
        }
    }

    void Update()
    {
        if (auraRenderer != null && auraRenderer.enabled)
        {
            if (auraRing)
            {
                // 挑発の輪は広がりながら薄れ、また足元から広がる
                float k = Mathf.Repeat(Time.time * 0.9f + auraPhase, 1f);
                auraRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.3f, k);
                auraRenderer.color = new Color(auraColor.r, auraColor.g, auraColor.b, 0.75f * (1f - k));
            }
            else
            {
                // 足元の光はゆっくり明滅
                auraRenderer.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
                float a = 0.3f + 0.12f * Mathf.Sin(Time.time * 2.2f + auraPhase);
                auraRenderer.color = new Color(auraColor.r, auraColor.g, auraColor.b, a);
            }
        }
    }
    private static bool IsAscii(string s)
    {
        foreach (char ch in s)
            if (ch > 0x7F) return false;
        return s.Length > 0;
    }
}
