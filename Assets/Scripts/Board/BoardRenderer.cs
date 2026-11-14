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

        // 罫線（成りゾーンの境界は少し太く）。細い線を1本ずつ置くと、画面の画素との重なり方で1本ごとに太さがばらつくので、
        // 1枚の画像に滑らかに描いてから貼る（縮むときはミップマップで均され、どの線も同じ太さに見える）
        float gridSpan = size + OuterLineWidth;
        CreateSprite("Grid", GridSprite(size, zone), center, new Vector2(gridSpan, gridSpan), Palette.BoardLine, OrderGrid);

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

    // 罫線の画像（盤の大きさごとに1枚。1マスを GridPixelsPerCell 画素で描く）
    private const int GridPixelsPerCell = 128;
    private static readonly System.Collections.Generic.Dictionary<int, Sprite> gridSprites = new System.Collections.Generic.Dictionary<int, Sprite>();

    /// <summary>
    /// 罫線を1枚の画像に描く。線の縁は画素にかかる割合で薄くする（アンチエイリアス）。
    /// 画像の左下が盤の (-0.5 - 外周の線の半分) にあたる
    /// </summary>
    private static Sprite GridSprite(int size, int zone)
    {
        if (gridSprites.TryGetValue(size * 100 + zone, out Sprite cached) && cached != null) return cached;
        float span = size + OuterLineWidth;
        int px = Mathf.CeilToInt(span * GridPixelsPerCell);
        float unitPerPx = span / px;
        float origin = -0.5f - OuterLineWidth * 0.5f;   // 画像の左端（下端）の盤の座標

        // 縦横とも同じ線なので、1列ぶんの「線のかかり具合」を求めて、縦と横で使い回す
        var lines = new System.Collections.Generic.List<Vector2>();   // (中心, 太さ)
        for (int i = 0; i <= size; i++)
        {
            bool outer = i == 0 || i == size;
            lines.Add(new Vector2(i - 0.5f, outer ? OuterLineWidth : LineWidth));
        }
        var zoneLines = new System.Collections.Generic.List<Vector2>
        {
            new Vector2(zone - 0.5f, LineWidth * 2.2f), new Vector2(size - zone - 0.5f, LineWidth * 2.2f)
        };
        float[] cover = Coverage(px, origin, unitPerPx, lines);
        float[] zoneCover = Coverage(px, origin, unitPerPx, zoneLines);

        var tex = new Texture2D(px, px, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 2;
        var pixels = new Color32[px * px];
        for (int y = 0; y < px; y++)
        {
            float rowLine = Mathf.Max(cover[y], zoneCover[y]);   // 横の線（成りゾーンの境界も）
            for (int x = 0; x < px; x++)
            {
                float a = Mathf.Max(rowLine, cover[x]);
                pixels[y * px + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(true, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), px / span);
        sprite.name = "BoardGrid" + size;
        gridSprites[size * 100 + zone] = sprite;
        return sprite;
    }

    /// <summary>1列の画素ごとに、線がどれだけかかっているか（0〜1）</summary>
    private static float[] Coverage(int px, float origin, float unitPerPx, System.Collections.Generic.List<Vector2> lines)
    {
        var cover = new float[px];
        for (int i = 0; i < px; i++)
        {
            float lo = origin + i * unitPerPx, hi = lo + unitPerPx;
            float best = 0f;
            foreach (var l in lines)
            {
                float a = l.x - l.y * 0.5f, b = l.x + l.y * 0.5f;
                float overlap = Mathf.Min(hi, b) - Mathf.Max(lo, a);
                if (overlap > 0f) best = Mathf.Max(best, overlap / unitPerPx);
            }
            cover[i] = best;
        }
        return cover;
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
        ViewFlip.Upright(obj.transform);
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
