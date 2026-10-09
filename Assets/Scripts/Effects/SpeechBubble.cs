using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 駒のセリフの吹き出し（「作れますよ」「成ったな……」など）。
/// 駒について動き、少しして消える。同じ駒の吹き出しは新しいものに置き換わる
/// </summary>
public class SpeechBubble : MonoBehaviour
{
    private const int OrderBorder = 86;
    private const int OrderPaper = 87;
    private const int OrderText = 88;
    private const float FontSize = 2.1f;
    private const float PopTime = 0.14f;
    private const float FadeTime = 0.25f;

    private static readonly Color Paper = new Color(0.99f, 0.96f, 0.89f, 0.97f);
    private static readonly Dictionary<PieceInstance, SpeechBubble> current = new Dictionary<PieceInstance, SpeechBubble>();

    private PieceInstance owner;
    private Transform follow;
    private Vector3 anchor;
    private Vector3 offset;
    private float life;
    private float elapsed;
    private SpriteRenderer[] sprites;
    private Color[] spriteColors;
    private TextMeshPro text;

    /// <summary>駒にセリフを言わせる</summary>
    public static void Say(PieceInstance piece, string line, float duration = 1.5f)
    {
        if (GameSim.Headless || piece == null || string.IsNullOrEmpty(line)) return;
        BoardManager bm = BoardManager.Instance;
        if (bm == null) return;

        SpeechBubble old;
        if (current.TryGetValue(piece, out old) && old != null) Destroy(old.gameObject);

        PieceController pc = bm.GetPieceController(piece.boardPosition);
        if (pc != null && pc.GetPiece() != piece) pc = null;
        bool below = piece.boardPosition.y >= bm.CurrentBoardSize - 1;   // 最上段の駒は下に出す

        var obj = new GameObject("SpeechBubble");
        if (BattleEffects.Instance != null) obj.transform.SetParent(BattleEffects.Instance.EffectsRoot, false);
        var bubble = obj.AddComponent<SpeechBubble>();
        bubble.Build(piece, line, pc != null ? pc.transform : null,
            new Vector3(piece.boardPosition.x, piece.boardPosition.y, 0f), below, duration);
        current[piece] = bubble;
    }

    /// <summary>候補からランダムに1つ言わせる</summary>
    public static void Say(PieceInstance piece, string[] lines, float duration = 1.5f)
    {
        if (GameSim.Headless || lines == null || lines.Length == 0) return;
        Say(piece, lines[Random.Range(0, lines.Length)], duration);
    }

    /// <summary>chance の確率で言わせる（頻繁に起きることで吹き出しだらけにならないように）</summary>
    public static void SayMaybe(PieceInstance piece, string[] lines, float chance)
    {
        if (GameSim.Headless || Random.value > chance) return;
        Say(piece, lines);
    }

    private void Build(PieceInstance piece, string line, Transform followTarget, Vector3 boardPos, bool below, float duration)
    {
        owner = piece;
        follow = followTarget;
        anchor = boardPos;
        life = duration;
        offset = new Vector3(0.2f, below ? -0.7f : 0.7f, 0f);

        // 文字
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(transform, false);
        text = textObj.AddComponent<TextMeshPro>();
        text.font = GameFonts.PieceTMP;
        text.fontSize = FontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = Palette.Ink;
        text.text = line;
        Vector2 size = text.GetPreferredValues(line);
        text.rectTransform.sizeDelta = size;
        var mr = textObj.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = OrderText;

        // 紙と縁（縁は陣営の色）
        Color edge = Palette.TeamColor(piece.team);
        Vector2 paperSize = new Vector2(size.x + 0.26f, size.y + 0.12f);
        var border = CreatePart("Border", SpriteFactory.RoundedRect, edge, OrderBorder);
        border.drawMode = SpriteDrawMode.Sliced;
        border.size = paperSize + new Vector2(0.05f, 0.05f);
        var paper = CreatePart("Paper", SpriteFactory.RoundedRect, Paper, OrderPaper);
        paper.drawMode = SpriteDrawMode.Sliced;
        paper.size = paperSize;

        // しっぽ（駒の方を向く小さな三角。45度回した四角を縁に半分埋める）
        float tailY = (below ? 1f : -1f) * (paperSize.y * 0.5f);
        var tailEdge = CreatePart("TailEdge", SpriteFactory.Pixel, edge, OrderBorder);
        tailEdge.transform.localPosition = new Vector3(-0.2f, tailY, 0f);
        tailEdge.transform.localRotation = Quaternion.Euler(0, 0, 45f);
        tailEdge.transform.localScale = new Vector3(0.17f, 0.17f, 1f);
        var tail = CreatePart("Tail", SpriteFactory.Pixel, Paper, OrderPaper);
        tail.transform.localPosition = new Vector3(-0.2f, tailY, 0f);
        tail.transform.localRotation = Quaternion.Euler(0, 0, 45f);
        tail.transform.localScale = new Vector3(0.12f, 0.12f, 1f);

        sprites = new[] { border, paper, tailEdge, tail };
        spriteColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++) spriteColors[i] = sprites[i].color;
        Place();
        transform.localScale = Vector3.one * 0.6f;
    }

    private SpriteRenderer CreatePart(string name, Sprite sprite, Color color, int order)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private void Place()
    {
        if (follow != null) anchor = new Vector3(follow.position.x, follow.position.y, 0f);
        transform.position = anchor + offset;
    }

    void LateUpdate()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= life)
        {
            Destroy(gameObject);
            return;
        }
        Place();

        // ぽんと出て、最後はふわっと消える
        float pop = elapsed < PopTime ? Ease.OutBack(elapsed / PopTime) : 1f;
        transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, pop);
        float alpha = elapsed > life - FadeTime ? (life - elapsed) / FadeTime : 1f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Color c = spriteColors[i];
            c.a *= alpha;
            sprites[i].color = c;
        }
        text.alpha = alpha;
    }

    void OnDestroy()
    {
        SpeechBubble b;
        if (owner != null && current.TryGetValue(owner, out b) && b == this) current.Remove(owner);
    }
}
