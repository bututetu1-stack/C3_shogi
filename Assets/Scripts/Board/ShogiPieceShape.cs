using UnityEngine;

public static class ShogiPieceShape
{
    // SVGパスから抽出した将棋駒の五角形頂点 (正規化: -1 to 1)
    // 五角形頂点 (正規化: -1 to 1)
    // テクスチャ座標系でy+が上なので、尖った先はy=+1(上)に配置
    private static readonly Vector2[] vertices = new Vector2[]
    {
        new Vector2(0f, 1f),           // 上頂点 (尖った先)
        new Vector2(-0.668f, 0.647f),  // 左上
        new Vector2(-0.836f, -1f),     // 左下 (底辺)
        new Vector2(0.836f, -1f),      // 右下 (底辺)
        new Vector2(0.668f, 0.647f),   // 右上
    };

    // レアリティごとの駒本体の色
    public static Color GetRarityBodyColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Bronze:    return new Color(0.72f, 0.45f, 0.2f);      // 銅 rgb(184,115,51)
            case Rarity.Normal:    return new Color(1f, 0.89f, 0.612f);       // 茶 rgb(255,227,156)
            case Rarity.Rare:      return new Color(0.741f, 0.765f, 0.788f);  // 銀 rgb(189,195,201)
            case Rarity.SuperRare: return new Color(0.792f, 0.659f, 0.275f);  // 金 rgb(202,168,70)
            case Rarity.Legend:    return new Color(0.604f, 0.384f, 0.161f);  // 特殊茶 rgb(154,98,41)
            default: return Color.white;
        }
    }

    public static Texture2D CreatePieceTexture(int size, Rarity rarity, bool flipped)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.filterMode = FilterMode.Trilinear;

        Color bodyColor = GetRarityBodyColor(rarity);
        Color outlineColor = new Color(0.267f, 0.267f, 0.267f); // rgb(68,68,68)
        Color clear = new Color(0, 0, 0, 0);

        // 全ピクセルをクリア
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        float margin = 0.08f;
        float scale = (1f - margin * 2f) * 0.5f;
        float cx = size * 0.5f;
        float cy = size * 0.5f;

        // 各ピクセルが五角形内かチェック
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - cx) / (size * scale);
                float ny = (y - cy) / (size * scale);
                if (flipped) { nx = -nx; ny = -ny; }

                float distToEdge = DistToPolygonEdge(nx, ny);
                bool inside = IsInsidePolygon(nx, ny);
                float signedDist = inside ? -distToEdge : distToEdge;
                // signedDist: 負=ポリゴン内部, 正=外部

                float outlineWidth = 2.5f / (size * scale);
                float aaRange = 2f / (size * scale);
                float innerEdge = -outlineWidth;

                if (signedDist < innerEdge - aaRange)
                {
                    pixels[y * size + x] = bodyColor;
                }
                else if (signedDist < innerEdge + aaRange)
                {
                    float t = Mathf.Clamp01((signedDist - innerEdge + aaRange) / (2f * aaRange));
                    pixels[y * size + x] = Color.Lerp(bodyColor, outlineColor, t);
                }
                else if (signedDist < -aaRange)
                {
                    pixels[y * size + x] = outlineColor;
                }
                else if (signedDist < aaRange)
                {
                    float t = Mathf.Clamp01((signedDist + aaRange) / (2f * aaRange));
                    pixels[y * size + x] = new Color(outlineColor.r, outlineColor.g, outlineColor.b, 1f - t);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static bool IsInsidePolygon(float px, float py)
    {
        int n = vertices.Length;
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float yi = vertices[i].y, yj = vertices[j].y;
            float xi = vertices[i].x, xj = vertices[j].x;
            if ((yi > py) != (yj > py) &&
                px < (xj - xi) * (py - yi) / (yj - yi) + xi)
                inside = !inside;
        }
        return inside;
    }

    private static float DistToPolygonEdge(float px, float py)
    {
        float minDist = float.MaxValue;
        int n = vertices.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float dist = DistToSegment(px, py, vertices[j].x, vertices[j].y, vertices[i].x, vertices[i].y);
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }

    private static float DistToSegment(float px, float py, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay;
        float t = ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy);
        t = Mathf.Clamp01(t);
        float cx = ax + t * dx - px;
        float cy = ay + t * dy - py;
        return Mathf.Sqrt(cx * cx + cy * cy);
    }

    // ステータスアイコン用テクスチャ生成
    public static Texture2D CreateStatIcon(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.filterMode = FilterMode.Trilinear;
        Color[] pixels = new Color[size * size];

        float center = size * 0.5f;
        float radius = size * 0.4f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= radius)
                {
                    float edge = radius - dist;
                    float alpha = Mathf.Clamp01(edge * 2f);
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, alpha);
                }
                else
                {
                    pixels[y * size + x] = new Color(0, 0, 0, 0);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
