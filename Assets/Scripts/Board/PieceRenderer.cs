using UnityEngine;
using TMPro;

public class PieceRenderer : MonoBehaviour
{
    public static readonly Color ATKColor = new Color(0.9f, 0.25f, 0.25f);
    public static readonly Color DEFColor = new Color(0.25f, 0.5f, 0.9f);
    public static readonly Color HPColor = new Color(0.2f, 0.8f, 0.3f);

    private static TMP_FontAsset tmpFont;

    private SpriteRenderer bodyRenderer;
    private TextMeshPro labelTop;
    private TextMeshPro labelBottom;
    private TextMeshPro atkText;
    private TextMeshPro defText;
    private TextMeshPro hpText;
    private SpriteRenderer atkIcon;
    private SpriteRenderer defIcon;
    private SpriteRenderer hpIcon;
    private PieceInstance pieceInstance;
    private bool isEnemyPiece;
    private Texture2D cachedIconTex;
    private Rarity appliedRarity;

    private const float LABEL_SIZE_1CHAR = 3.5f;
    private const float LABEL_SIZE_2CHAR = 2.3f;
    private const float STAT_FONT_SIZE = 1.5f;

    private static TMP_FontAsset GetTMPFont()
    {
        if (tmpFont == null)
            tmpFont = Resources.Load<TMP_FontAsset>("NotoSansJP-TMP");
        return tmpFont;
    }

    public void Init(PieceInstance piece)
    {
        pieceInstance = piece;
        bool isEnemy = (piece.team == Team.Enemy);

        int texSize = 512;
        appliedRarity = piece.CurrentRarity;
        Texture2D tex = ShogiPieceShape.CreatePieceTexture(texSize, appliedRarity, isEnemy);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), texSize);

        bodyRenderer = gameObject.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = sprite;
        bodyRenderer.sortingOrder = 1;

        transform.localScale = new Vector3(0.95f, 0.95f, 1f);

        string displayName = piece.DisplayName;
        Color labelColor = piece.isPromoted ? new Color(0.85f, 0.1f, 0.1f) : new Color(0.1f, 0.1f, 0.1f);

        if (displayName.Length == 1)
        {
            CreateLabel("LabelTop", displayName, new Vector3(0f, 0.02f, -0.1f), isEnemy, out labelTop, labelColor, LABEL_SIZE_1CHAR);
            CreateLabel("LabelBottom", "", new Vector3(0f, -0.14f, -0.1f), isEnemy, out labelBottom, labelColor, LABEL_SIZE_1CHAR);
        }
        else
        {
            string topChar = displayName.Substring(0, 1);
            string bottomChar = displayName.Length > 1 ? displayName.Substring(1, 1) : "";
            if (isEnemy)
            {
                CreateLabel("LabelTop", bottomChar, new Vector3(0f, 0.18f, -0.1f), true, out labelTop, labelColor, LABEL_SIZE_2CHAR);
                CreateLabel("LabelBottom", topChar, new Vector3(0f, -0.14f, -0.1f), true, out labelBottom, labelColor, LABEL_SIZE_2CHAR);
            }
            else
            {
                CreateLabel("LabelTop", topChar, new Vector3(0f, 0.18f, -0.1f), false, out labelTop, labelColor, LABEL_SIZE_2CHAR);
                CreateLabel("LabelBottom", bottomChar, new Vector3(0f, -0.14f, -0.1f), false, out labelBottom, labelColor, LABEL_SIZE_2CHAR);
            }
        }

        isEnemyPiece = isEnemy;
        Texture2D iconTex = ShogiPieceShape.CreateStatIcon(128, Color.white);
        cachedIconTex = iconTex;

        if (piece.ATK > 0)
        {
            Vector3 atkPos = isEnemy ? new Vector3(0.30f, -0.30f, -0.1f) : new Vector3(-0.30f, 0.30f, -0.1f);
            CreateStatDisplay("ATK", piece.ATK, ATKColor, iconTex, atkPos, isEnemy, out atkIcon, out atkText);
        }

        if (piece.DEF > 0)
        {
            Vector3 defPos = isEnemy ? new Vector3(-0.30f, -0.30f, -0.1f) : new Vector3(0.30f, 0.30f, -0.1f);
            CreateStatDisplay("DEF", piece.DEF, DEFColor, iconTex, defPos, isEnemy, out defIcon, out defText);
        }

        if (piece.currentHP > 0)
        {
            Vector3 hpPos = isEnemy ? new Vector3(-0.30f, 0.35f, -0.1f) : new Vector3(0.30f, -0.35f, -0.1f);
            CreateStatDisplay("HP", piece.currentHP, HPColor, iconTex, hpPos, isEnemy, out hpIcon, out hpText);
            if (hpText != null) hpText.text = FormatHP(piece.currentHP);
        }
    }

    private void CreateLabel(string objName, string text, Vector3 localPos, bool rotate180,
        out TextMeshPro textMesh, Color color, float fontSize)
    {
        GameObject obj = new GameObject(objName);
        obj.transform.parent = transform;
        obj.transform.localPosition = localPos;
        if (rotate180)
            obj.transform.localRotation = Quaternion.Euler(0, 0, 180);
        else
            obj.transform.localRotation = Quaternion.identity;

        textMesh = obj.AddComponent<TextMeshPro>();
        textMesh.text = text;
        textMesh.fontSize = fontSize;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.color = (color.a > 0f) ? color : new Color(0.1f, 0.1f, 0.1f);
        textMesh.fontStyle = FontStyles.Bold;
        textMesh.textWrappingMode = TextWrappingModes.NoWrap;
        textMesh.overflowMode = TextOverflowModes.Overflow;

        TMP_FontAsset font = GetTMPFont();
        if (font != null)
        {
            textMesh.font = font;
            // FaceDilate で文字の線を太くする
            Material boldMat = new Material(font.material);
            boldMat.SetFloat("_FaceDilate", 0.2f);
            textMesh.fontSharedMaterial = boldMat;
        }

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1f, 0.5f);

        MeshRenderer mr = obj.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 2;
    }

    private void CreateStatDisplay(string name, int value, Color color, Texture2D iconTex,
        Vector3 localPos, bool isEnemy, out SpriteRenderer icon, out TextMeshPro text)
    {
        // Icon sprite
        GameObject iconObj = new GameObject(name + "Icon");
        iconObj.transform.parent = transform;
        iconObj.transform.localPosition = localPos;
        iconObj.transform.localScale = new Vector3(0.15f, 0.15f, 1f);

        icon = iconObj.AddComponent<SpriteRenderer>();
        Sprite iconSprite = Sprite.Create(iconTex, new Rect(0, 0, iconTex.width, iconTex.width), new Vector2(0.5f, 0.5f), iconTex.width);
        icon.sprite = iconSprite;
        icon.color = color;
        icon.sortingOrder = 3;

        // Text - direct child of piece (not icon) to avoid scale distortion
        GameObject textObj = new GameObject(name + "Text");
        textObj.transform.parent = transform;
        textObj.transform.localPosition = new Vector3(localPos.x, localPos.y, localPos.z - 0.5f);

        text = textObj.AddComponent<TextMeshPro>();
        text.text = value.ToString();
        text.fontSize = STAT_FONT_SIZE;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;

        TMP_FontAsset font = GetTMPFont();
        if (font != null) text.font = font;

        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0.5f, 0.4f);

        MeshRenderer mr = textObj.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 4;
    }

    private static string FormatHP(int hp)
    {
        return hp >= 999 ? "\u221E" : hp.ToString();
    }

    public void UpdateHP()
    {
        if (pieceInstance == null) return;
        if (hpText != null) hpText.text = FormatHP(pieceInstance.currentHP);
    }

    public void UpdateAllStats()
    {
        if (pieceInstance == null) return;

        // 成り・成り解除でレアリティが変わったら駒の地色を差し替える
        if (pieceInstance.CurrentRarity != appliedRarity)
        {
            appliedRarity = pieceInstance.CurrentRarity;
            int texSize = 512;
            Texture2D newTex = ShogiPieceShape.CreatePieceTexture(texSize, appliedRarity, isEnemyPiece);
            Sprite newSprite = Sprite.Create(newTex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), texSize);
            if (bodyRenderer != null) bodyRenderer.sprite = newSprite;
        }

        Vector3 atkPos = isEnemyPiece ? new Vector3(0.30f, -0.30f, -0.1f) : new Vector3(-0.30f, 0.30f, -0.1f);
        EnsureStatDisplay(ref atkIcon, ref atkText, "ATK", pieceInstance.ATK, ATKColor, atkPos);

        Vector3 defPos = isEnemyPiece ? new Vector3(-0.30f, -0.30f, -0.1f) : new Vector3(0.30f, 0.30f, -0.1f);
        EnsureStatDisplay(ref defIcon, ref defText, "DEF", pieceInstance.DEF, DEFColor, defPos);

        Vector3 hpPos = isEnemyPiece ? new Vector3(-0.30f, 0.35f, -0.1f) : new Vector3(0.30f, -0.35f, -0.1f);
        EnsureStatDisplay(ref hpIcon, ref hpText, "HP", pieceInstance.currentHP, HPColor, hpPos);

        Color labelColor = pieceInstance.isPromoted ? new Color(0.85f, 0.1f, 0.1f) : new Color(0.1f, 0.1f, 0.1f);
        if (labelTop != null) labelTop.color = labelColor;
        if (labelBottom != null) labelBottom.color = labelColor;

        string displayName = pieceInstance.DisplayName;
        if (displayName.Length == 1)
        {
            if (labelTop != null)
            {
                labelTop.text = displayName;
                labelTop.gameObject.transform.localPosition = new Vector3(0f, 0.02f, -0.1f);
                labelTop.fontSize = LABEL_SIZE_1CHAR;
            }
            if (labelBottom != null) labelBottom.text = "";
        }
        else
        {
            string topChar = displayName.Substring(0, 1);
            string bottomChar = displayName.Length > 1 ? displayName.Substring(1, 1) : "";
            if (labelTop != null)
            {
                labelTop.gameObject.transform.localPosition = new Vector3(0f, 0.18f, -0.1f);
                labelTop.fontSize = LABEL_SIZE_2CHAR;
            }
            if (labelBottom != null) labelBottom.fontSize = LABEL_SIZE_2CHAR;
            if (isEnemyPiece)
            {
                if (labelTop != null) labelTop.text = bottomChar;
                if (labelBottom != null) labelBottom.text = topChar;
            }
            else
            {
                if (labelTop != null) labelTop.text = topChar;
                if (labelBottom != null) labelBottom.text = bottomChar;
            }
        }
    }

    private void EnsureStatDisplay(ref SpriteRenderer icon, ref TextMeshPro text,
        string statName, int value, Color color, Vector3 pos)
    {
        if (value > 0 && text == null)
        {
            if (cachedIconTex == null)
                cachedIconTex = ShogiPieceShape.CreateStatIcon(128, Color.white);
            CreateStatDisplay(statName, value, color, cachedIconTex, pos, isEnemyPiece, out icon, out text);
        }
        if (text != null)
        {
            text.text = (statName == "HP") ? FormatHP(value) : value.ToString();
            if (icon != null) icon.gameObject.SetActive(value > 0);
            text.gameObject.SetActive(value > 0);
        }
    }

    public PieceInstance GetPieceInstance()
    {
        return pieceInstance;
    }
}
