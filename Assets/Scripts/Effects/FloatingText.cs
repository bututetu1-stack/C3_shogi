using UnityEngine;
using TMPro;

/// <summary>ダメージ・回復・バフなどを駒の上に浮かべて表示する</summary>
public class FloatingText : MonoBehaviour
{
    private TextMeshPro text;
    private Vector3 start;
    private float duration;
    private float elapsed;
    private Color color;

    public static void Spawn(Vector2Int boardPos, string message, Color color, float size = 3.2f, float delay = 0f)
    {
        Spawn(new Vector3(boardPos.x, boardPos.y + 0.15f, 0f), message, color, size, delay);
    }

    public static void Spawn(Vector3 worldPos, string message, Color color, float size = 3.2f, float delay = 0f)
    {
        if (GameSim.Headless) return;
        var obj = new GameObject("FloatingText");
        if (BattleEffects.Instance != null) obj.transform.SetParent(BattleEffects.Instance.EffectsRoot, false);
        obj.transform.position = worldPos;

        var ft = obj.AddComponent<FloatingText>();
        ft.text = obj.AddComponent<TextMeshPro>();
        ft.text.text = message;
        ft.text.font = GameFonts.NumberTMP;
        ft.text.fontSharedMaterial = GameFonts.NumberOutlineMaterial;
        ft.text.fontSize = size;
        ft.text.alignment = TextAlignmentOptions.Center;
        ft.text.textWrappingMode = TextWrappingModes.NoWrap;
        ft.text.color = color;
        ft.text.rectTransform.sizeDelta = new Vector2(3f, 1f);
        var mr = obj.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 80;

        ft.start = worldPos;
        ft.duration = 0.9f;
        ft.elapsed = -delay;
        ft.color = color;
        if (delay > 0f) ft.text.alpha = 0f;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed < 0f) return;

        float t = elapsed / duration;
        if (t >= 1f) { Destroy(gameObject); return; }

        // ぽんと出て、ふわっと上がって消える
        float pop = t < 0.15f ? Ease.OutBack(t / 0.15f) : 1f;
        transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, pop);
        transform.position = start + new Vector3(0f, Ease.OutCubic(t) * 0.55f, 0f);
        Color c = color;
        c.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
        text.color = c;
    }
}
