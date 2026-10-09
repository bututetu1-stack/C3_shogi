using UnityEngine;
using TMPro;

/// <summary>
/// 将棋盤の見た目を組み立てる（背景・盤の落ち影・枠・木目・陣地の色・罫線・星・座標・マス）。
/// マスの中心がワールド座標の整数位置になる。
/// </summary>
public class BoardRenderer : MonoBehaviour
{
    // 描画順
    private const int OrderBackground = -100;
    private const int OrderShadow = -20;
    private const int OrderFrame = -10;
    private const int OrderWood = -5;
    private const int OrderZone = 0;
    private const int OrderGrid = 1;
    private const int OrderStar = 2;
    private const int OrderCoord = 2;

    private const float WoodMargin = 0.14f;     // 外周の罫線から木目の端まで
    private const float BoardThickness = 0.18f; // 盤の厚み（側面）
    private const float CoordGap = 0.36f;       // 盤の端から座標表記まで
    private const float LineWidth = 0.022f;
    private const float OuterLineWidth = 0.05f;

    private static readonly string[] KanjiNumbers = { "一", "二", "三", "四", "五", "六", "七", "八", "九" };
    private static readonly string[] WideDigits = { "１", "２", "３", "４", "５", "６", "７", "８", "９" };

    private int boardSize;
    private BoardCell[,] cells;
    private Transform boardRoot;

    public void BuildBoard(int size)
    {
        ClearBoard();

        boardSize = size;
        cells = new BoardCell[size, size];

        boardRoot = new GameObject("BoardVisual").transform;
        boardRoot.SetParent(transform, false);

        float c = (size - 1) * 0.5f;
        float half = size * 0.5f;
        Vector3 center = new Vector3(c, c, 0f);

        // 背景のほのかな光
        var glow = CreateSprite("BackgroundGlow", SpriteFactory.BackgroundGlow, center, new Vector2(size * 3.2f, size * 3.2f), Palette.BackgroundGlow, OrderBackground);
        glow.color = new Color(Palette.BackgroundGlow.r, Palette.BackgroundGlow.g, Palette.BackgroundGlow.b, 0.85f);

        // 盤の落ち影
        float woodSize = size + WoodMargin * 2f;
        CreateSliced("Shadow", SpriteFactory.SoftRect, center + new Vector3(0.18f, -0.42f, 0f),
            new Vector2(woodSize + 1.0f, woodSize + 1.1f), new Color(0f, 0f, 0f, 0.7f), OrderShadow, 1f);

        // 盤の厚み（側面が下に見える）と縁
        CreateSprite("Side", SpriteFactory.Pixel, center + new Vector3(0f, -BoardThickness * 0.5f, 0f),
            new Vector2(woodSize, woodSize + BoardThickness), Palette.BoardFrame, OrderFrame);
        CreateSprite("SideShade", SpriteFactory.Pixel, center + new Vector3(0f, -woodSize * 0.5f - BoardThickness * 0.5f, 0f),
            new Vector2(woodSize, BoardThickness), new Color(0f, 0f, 0f, 0.25f), OrderFrame + 1);
        CreateSprite("Edge", SpriteFactory.Pixel, center, new Vector2(woodSize + 0.05f, woodSize + 0.05f), Palette.BoardFrameDark, OrderFrame + 2);

        // 木目の盤面
        CreateSprite("Wood", SpriteFactory.BoardWood, center, new Vector2(woodSize, woodSize), Color.white, OrderWood);
        // 陣地（成りゾーン）の色: 上=敵陣（朱）、下=自陣（藍）
        int zone = BoardCell.GetPromoteRows(size);
        float zoneY = zone * 0.5f;
        CreateSprite("EnemyZone", SpriteFactory.Pixel, new Vector3(c, size - zoneY - 0.5f, 0f), new Vector2(size, zone), Palette.EnemyZoneTint, OrderZone);
        CreateSprite("PlayerZone", SpriteFactory.Pixel, new Vector3(c, zoneY - 0.5f, 0f), new Vector2(size, zone), Palette.PlayerZoneTint, OrderZone);

        // 罫線
        for (int i = 0; i <= size; i++)
        {
            float p = i - 0.5f;
            bool outer = i == 0 || i == size;
            float w = outer ? OuterLineWidth : LineWidth;
            float len = size + (outer ? OuterLineWidth : 0f);
            CreateSprite("V" + i, SpriteFactory.Pixel, new Vector3(p, c, 0f), new Vector2(w, len), Palette.BoardLine, OrderGrid);
            CreateSprite("H" + i, SpriteFactory.Pixel, new Vector3(c, p, 0f), new Vector2(len, w), Palette.BoardLine, OrderGrid);
        }

        // 成りゾーンの境界線を少し太く
        float zoneLineW = LineWidth * 2.2f;
        CreateSprite("ZoneLineTop", SpriteFactory.Pixel, new Vector3(c, size - zone - 0.5f, 0f), new Vector2(size, zoneLineW), Palette.BoardLine, OrderGrid);
        CreateSprite("ZoneLineBottom", SpriteFactory.Pixel, new Vector3(c, zone - 0.5f, 0f), new Vector2(size, zoneLineW), Palette.BoardLine, OrderGrid);

        // 星（9路のみ：3筋目と6筋目の交点）
        if (size == 9)
        {
            float[] stars = { 2.5f, 5.5f };
            foreach (float sx in stars)
                foreach (float sy in stars)
                    CreateSprite("Star", SpriteFactory.Circle, new Vector3(sx, sy, 0f), new Vector2(0.11f, 0.11f), Palette.BoardLine, OrderStar);
        }

        // 座標（上辺に筋＝右から１..、右辺に段＝上から一..）
        float labelOffset = half + WoodMargin + CoordGap;
        for (int x = 0; x < size; x++)
        {
            int suji = size - x; // 右端が1筋
            CreateLabel(WideDigits[suji - 1], new Vector3(x, c + labelOffset, 0f), GameFonts.NumberTMP, null, 2.1f);
        }
        for (int y = 0; y < size; y++)
        {
            int dan = size - y; // 上端が一段
            CreateLabel(KanjiNumbers[dan - 1], new Vector3(c + labelOffset, y, 0f), GameFonts.PieceTMP, GameFonts.PieceLabelMaterial, 2.1f);
        }

        // マス（ハイライト用）
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                GameObject cellObj = new GameObject("Cell_" + x + "_" + y);
                cellObj.transform.SetParent(boardRoot, false);
                cellObj.transform.position = new Vector3(x, y, 0);
                BoardCell cell = cellObj.AddComponent<BoardCell>();
                cell.Init(new Vector2Int(x, y));
                cells[x, y] = cell;
            }
        }

        // カメラを盤に合わせる
        CameraFitter fitter = CameraFitter.Ensure();
        if (fitter != null) fitter.Fit(size);
    }

    private void ClearBoard()
    {
        if (boardRoot != null) Destroy(boardRoot.gameObject);
        boardRoot = null;
        cells = null;
    }

    public BoardCell GetCell(Vector2Int pos)
    {
        if (cells == null || pos.x < 0 || pos.x >= boardSize || pos.y < 0 || pos.y >= boardSize)
            return null;
        return cells[pos.x, pos.y];
    }

    private SpriteRenderer CreateSprite(string name, Sprite sprite, Vector3 pos, Vector2 size, Color color, int order)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(boardRoot, false);
        obj.transform.position = pos;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        Vector2 spriteSize = sprite.bounds.size;
        obj.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
        return sr;
    }

    /// <summary>スライス描画のスプライト。k が大きいほど角の丸み・ぼかしが小さくなる</summary>
    private SpriteRenderer CreateSliced(string name, Sprite sprite, Vector3 pos, Vector2 size, Color color, int order, float k)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(boardRoot, false);
        obj.transform.position = pos;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Sliced;
        obj.transform.localScale = new Vector3(1f / k, 1f / k, 1f);
        sr.size = size * k;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private void CreateLabel(string text, Vector3 pos, TMP_FontAsset font, Material material, float fontSize)
    {
        var obj = new GameObject("Coord_" + text);
        obj.transform.SetParent(boardRoot, false);
        obj.transform.position = pos;
        var tmp = obj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.font = font;
        if (material != null) tmp.fontSharedMaterial = material;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.color = Palette.CoordText;
        tmp.rectTransform.sizeDelta = new Vector2(0.6f, 0.6f);
        var mr = obj.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = OrderCoord;
    }
}
