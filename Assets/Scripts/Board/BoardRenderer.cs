using UnityEngine;

public class BoardRenderer : MonoBehaviour
{
    private int boardSize;
    private BoardCell[,] cells;
    private Sprite cellSprite;

    public int CurrentBoardSize { get { return boardSize; } }

    public void BuildBoard(int size)
    {
        // 既存セルをクリア
        ClearBoard();

        boardSize = size;
        cells = new BoardCell[size, size];
        CreateCellSprite();

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                GameObject cellObj = new GameObject("Cell_" + x + "_" + y);
                cellObj.transform.parent = transform;
                cellObj.transform.position = new Vector3(x, y, 0);

                SpriteRenderer sr = cellObj.AddComponent<SpriteRenderer>();
                sr.sprite = cellSprite;
                sr.sortingOrder = 0;

                cellObj.transform.localScale = new Vector3(0.95f, 0.95f, 1f);
                cellObj.AddComponent<BoxCollider2D>();

                BoardCell cell = cellObj.AddComponent<BoardCell>();
                cell.Init(new Vector2Int(x, y), size);
                cells[x, y] = cell;
            }
        }
    }

    private void ClearBoard()
    {
        if (cells == null) return;
        for (int x = 0; x < cells.GetLength(0); x++)
            for (int y = 0; y < cells.GetLength(1); y++)
                if (cells[x, y] != null)
                    Destroy(cells[x, y].gameObject);
    }

    private void CreateCellSprite()
    {
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        cellSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }

    public BoardCell GetCell(Vector2Int pos)
    {
        if (cells == null || pos.x < 0 || pos.x >= boardSize || pos.y < 0 || pos.y >= boardSize)
            return null;
        return cells[pos.x, pos.y];
    }

    public BoardCell GetCell(int x, int y)
    {
        return GetCell(new Vector2Int(x, y));
    }

    public void ResetAllHighlights()
    {
        if (cells == null) return;
        for (int x = 0; x < boardSize; x++)
            for (int y = 0; y < boardSize; y++)
                if (cells[x, y] != null)
                    cells[x, y].ResetColor();
    }
}
