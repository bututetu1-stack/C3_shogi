using UnityEngine;

public class BoardCell : MonoBehaviour
{
    public Vector2Int position;
    private SpriteRenderer spriteRenderer;
    private Color defaultColor;

    public static readonly Color BoardColor = new Color(0.82f, 0.66f, 0.43f);
    public static readonly Color PlayerPromoteZone = new Color(0.55f, 0.65f, 0.85f);
    public static readonly Color EnemyPromoteZone = new Color(0.85f, 0.55f, 0.55f);
    public static readonly Color HighlightMove = new Color(0.4f, 0.85f, 0.4f, 0.8f);
    public static readonly Color HighlightAttack = new Color(1f, 0.3f, 0.3f, 0.8f);
    public static readonly Color HighlightSelected = new Color(1f, 1f, 0.4f, 0.8f);

    public void Init(Vector2Int pos, int boardSize)
    {
        position = pos;
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 成りゾーンは盤サイズに応じて動的に計算
        int promoteRows = GetPromoteRows(boardSize);
        if (pos.y >= boardSize - promoteRows)
            defaultColor = PlayerPromoteZone;
        else if (pos.y < promoteRows)
            defaultColor = EnemyPromoteZone;
        else
            defaultColor = BoardColor;

        spriteRenderer.color = defaultColor;
    }

    public void SetHighlight(Color color)
    {
        spriteRenderer.color = color;
    }

    public void ResetColor()
    {
        spriteRenderer.color = defaultColor;
    }

    public static int GetPromoteRows(int boardSize)
    {
        return boardSize >= 9 ? 3 : 2;
    }
}
