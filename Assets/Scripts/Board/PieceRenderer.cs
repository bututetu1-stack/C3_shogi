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
    private Sprite auraArt;          // 深海棲姫の妖気（Resources/Effects/HimeAura.png があるとき）
    private SpriteRenderer flagRenderer;   // 深海の旗艦の旗
    private SpriteRenderer fireRenderer;   // 大破した艦娘の炎
    private Sprite fireArt;
    private bool fireIsMuzzle;
    private float smokeTimer;
    private float emberTimer;
    private Transform starRoot;            // 部員の練度の★（駒から見て左下）
    private int shownStars = -1;
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

        // 旗艦の旗は、バッジのない角（駒から見て左下。敵駒なら画面の右上）に立てる
        var flagObj = CreateChild("FlagshipMark", transform);
        flagObj.localPosition = new Vector3(-0.36f, -0.3f, 0f) * flip + new Vector3(0f, 0.08f, 0f);
        flagRenderer = flagObj.gameObject.AddComponent<SpriteRenderer>();
        flagRenderer.sortingOrder = OrderBadge;
        flagRenderer.enabled = false;

        // 練度の★（自軍の部員だけ。駒から見て左下の空いた角に並べる）
        starRoot = CreateChild("Stars", transform);
        starRoot.localPosition = new Vector3(-0.38f, -0.4f, 0f) * flip;

        // 大破の炎（艦娘は自軍なので、駒から見て左下の角から燃え上がる）
        var fireObj = CreateChild("DamageFire", visual);
        fireObj.localPosition = new Vector3(-0.28f, -0.06f, 0f);
        fireRenderer = fireObj.gameObject.AddComponent<SpriteRenderer>();
        fireRenderer.sortingOrder = OrderBadge;
        fireRenderer.enabled = false;

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
        bool kai2 = pieceInstance.kai2 || pieceInstance.IsVeteran;
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
        auraArt = pieceInstance.data.pieceType == PieceType.ShinkaiHime ? EffectArt.Get("HimeAura") : null;
        if (auraArt != null)
        {
            auraRenderer.enabled = true;
            auraRenderer.sprite = auraArt;
        }
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
            if (auraArt != null)
            {
                // 姫級の妖気はゆっくり渦を巻いて脈打つ
                auraRenderer.transform.localScale = Vector3.one * (1.6f + 0.08f * Mathf.Sin(Time.time * 2.4f + auraPhase));
                auraRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -Time.time * 25f);
                auraRenderer.color = new Color(1f, 1f, 1f, 0.85f);
            }
            else if (auraRing)
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
        UpdateFleetMarks();
        UpdateStars();
    }

    /// <summary>部員の練度が上がったら★を並べ直す</summary>
    private void UpdateStars()
    {
        if (starRoot == null || pieceInstance == null || pieceInstance.team != Team.Player || GameManager.Instance == null) return;
        int stars = pieceInstance.isAlive ? GameManager.Instance.Roster.StarsOf(pieceInstance.data.pieceType) : 0;
        if (stars == shownStars) return;
        shownStars = stars;
        for (int i = starRoot.childCount - 1; i >= 0; i--) Destroy(starRoot.GetChild(i).gameObject);
        for (int i = 0; i < stars; i++)
        {
            var shadow = CreateChild("StarShadow", starRoot);
            shadow.localPosition = new Vector3(i * 0.19f, -0.008f, 0f);
            shadow.localScale = new Vector3(0.25f, 0.25f, 1f);
            var sr0 = shadow.gameObject.AddComponent<SpriteRenderer>();
            sr0.sprite = SpriteFactory.Star;
            sr0.color = new Color(0.12f, 0.08f, 0.02f, 0.85f);
            sr0.sortingOrder = OrderBadge;
            var star = CreateChild("Star", starRoot);
            star.localPosition = new Vector3(i * 0.19f, 0f, 0f);
            star.localScale = new Vector3(0.19f, 0.19f, 1f);
            var sr = star.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Star;
            sr.color = Palette.GoldLight;
            sr.sortingOrder = OrderBadgeText;
        }
    }

    /// <summary>旗艦の旗と、艦娘の損傷（中破は黒煙、大破は炎と濃い黒煙）</summary>
    private void UpdateFleetMarks()
    {
        if (pieceInstance == null || flagRenderer == null) return;
        bool alive = pieceInstance.isAlive;

        bool flag = alive && pieceInstance.isFlagship;
        if (flagRenderer.enabled != flag)
        {
            flagRenderer.enabled = flag;
            if (flag)
            {
                flagRenderer.sprite = SpriteFactory.FlagshipMark;
                flagRenderer.color = EffectArt.Has("FlagshipMark") ? Color.white : new Color(0.86f, 0.2f, 0.18f);
            }
        }
        if (flag)
        {
            // 旗が風にはためく
            float k = Mathf.Sin(Time.time * 3.1f + auraPhase);
            flagRenderer.transform.localScale = new Vector3(0.44f * (1f + 0.07f * k), 0.44f, 1f);
            flagRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, 4f * k);
        }

        bool ship = alive && PieceTypes.IsKanmusu(pieceInstance.data.pieceType);
        bool damaged = ship && AbilitySystem.IsDamaged(pieceInstance);
        bool heavy = damaged && AbilitySystem.IsHeavilyDamaged(pieceInstance);
        if (fireRenderer.enabled != heavy)
        {
            fireRenderer.enabled = heavy;
            if (heavy)
            {
                // 画像がなければ発砲炎を上に向けて炎の代わりにする（それもなければ光の玉）
                fireArt = EffectArt.Get("DamageFire");
                fireIsMuzzle = fireArt == null && EffectArt.Has("MuzzleFlash");
                if (fireIsMuzzle) fireArt = EffectArt.Get("MuzzleFlash");
                fireRenderer.sprite = fireArt != null ? fireArt : SpriteFactory.SoftCircle;
                fireRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, fireIsMuzzle ? 90f : 0f);
            }
        }
        if (!damaged || BattleEffects.Instance == null) return;

        if (heavy)
        {
            // 炎はゆらめき、火の粉が舞う
            float f = Mathf.PerlinNoise(Time.time * 7f, auraPhase);
            if (fireIsMuzzle)
            {
                // 回した発砲炎は x が高さになる
                fireRenderer.transform.localScale = new Vector3(0.5f + 0.14f * f, 0.42f + 0.06f * f, 1f);
                fireRenderer.color = new Color(1f, 1f, 1f, 0.8f + 0.2f * f);
            }
            else if (fireArt != null)
            {
                fireRenderer.transform.localScale = new Vector3(0.6f + 0.05f * f, 0.6f + 0.14f * f, 1f);
                fireRenderer.color = new Color(1f, 1f, 1f, 0.85f + 0.15f * f);
            }
            else
            {
                fireRenderer.transform.localScale = new Vector3(0.3f + 0.06f * f, 0.38f + 0.12f * f, 1f);
                fireRenderer.color = new Color(1f, 0.45f + 0.3f * f, 0.12f, 0.75f + 0.25f * f);
            }
            emberTimer -= Time.deltaTime;
            if (emberTimer <= 0f)
            {
                emberTimer = Random.Range(0.12f, 0.3f);
                BattleEffects.Instance.PlayEmber(fireRenderer.transform.position + new Vector3(0f, 0.08f, 0f));
            }
        }

        smokeTimer -= Time.deltaTime;
        if (smokeTimer <= 0f)
        {
            smokeTimer = heavy ? 0.15f : 0.3f;
            BattleEffects.Instance.PlayDamageSmoke(transform.position + new Vector3(Random.Range(-0.18f, 0.18f), 0.36f, 0f), heavy);
        }
    }
    private static bool IsAscii(string s)
    {
        foreach (char ch in s)
            if (ch > 0x7F) return false;
        return s.Length > 0;
    }
}
