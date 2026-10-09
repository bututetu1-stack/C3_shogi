using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 手続き生成のスプライトを作ってキャッシュする（同じ見た目は1回だけ生成して使い回す）。
/// 画像素材が届いたら、ここを差し替えればゲーム全体の見た目が変わる。
/// </summary>
public static class SpriteFactory
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // 将棋駒の五角形（正規化座標 -1..1、尖った先が上）
    private static readonly Vector2[] PieceVerts =
    {
        new Vector2(0f, 1f),
        new Vector2(-0.70f, 0.66f),
        new Vector2(-0.86f, -1f),
        new Vector2(0.86f, -1f),
        new Vector2(0.70f, 0.66f),
    };

    // ------------------------------------------------------------
    // 基本図形
    // ------------------------------------------------------------

    /// <summary>1ユニット四方の白い四角（線や矩形用）</summary>
    public static Sprite Pixel
    {
        get
        {
            return Cached("pixel", () =>
            {
                var tex = NewTexture(4, 4, false);
                tex.filterMode = FilterMode.Point;
                Fill(tex, Color.white);
                return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            });
        }
    }

    /// <summary>中心から外へ透明になるぼかし円（光・パーティクル用）</summary>
    public static Sprite SoftCircle
    {
        get
        {
            return Cached("softcircle", () => Radial(64, (r) =>
            {
                float a = Mathf.Clamp01(1f - r);
                return a * a;
            }));
        }
    }

    /// <summary>縁の滑らかな塗り円</summary>
    public static Sprite Circle
    {
        get { return Cached("circle", () => Radial(64, (r) => Mathf.Clamp01((1f - r) * 32f))); }
    }

    /// <summary>リング（攻撃対象の目印・衝撃波用）</summary>
    public static Sprite Ring
    {
        get
        {
            return Cached("ring", () => Radial(128, (r) =>
            {
                float d = Mathf.Abs(r - 0.86f);
                return Mathf.Clamp01((0.1f - d) * 64f);
            }));
        }
    }

    /// <summary>角丸の四角（スライス用。SpriteRenderer.drawMode = Sliced で任意サイズに）</summary>
    public static Sprite RoundedRect
    {
        get
        {
            return Cached("roundrect", () =>
            {
                const int size = 64;
                const float radius = 20f;
                var tex = NewTexture(size, size, false);
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = RoundRectDistance(x + 0.5f, y + 0.5f, size, size, radius);
                        px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
                    }
                tex.SetPixels(px);
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size,
                    0, SpriteMeshType.FullRect, new Vector4(22, 22, 22, 22));
            });
        }
    }

    /// <summary>ぼけた角丸の四角（盤の落ち影用、スライス）</summary>
    public static Sprite SoftRect
    {
        get
        {
            return Cached("softrect", () =>
            {
                const int size = 64;
                const float blur = 20f;
                var tex = NewTexture(size, size, false);
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = RoundRectDistance(x + 0.5f, y + 0.5f, size, size, blur + 4f);
                        float a = Mathf.Clamp01(-d / blur);
                        px[y * size + x] = new Color(1, 1, 1, a * a * (3f - 2f * a));
                    }
                tex.SetPixels(px);
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size,
                    0, SpriteMeshType.FullRect, new Vector4(28, 28, 28, 28));
            });
        }
    }

    /// <summary>四隅のカギ括弧（選択枠・攻撃対象の枠）</summary>
    public static Sprite CornerBrackets
    {
        get
        {
            return Cached("brackets", () =>
            {
                const int size = 128;
                const float thick = 9f;
                const float len = 38f;
                const float inset = 6f;
                var tex = NewTexture(size, size, true);
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float fx = Mathf.Min(x + 0.5f, size - x - 0.5f) - inset; // 端からの距離
                        float fy = Mathf.Min(y + 0.5f, size - y - 0.5f) - inset;
                        float a = 0f;
                        if (fx >= 0 && fy >= 0)
                        {
                            bool horiz = fy < thick && fx < len;
                            bool vert = fx < thick && fy < len;
                            if (horiz || vert) a = 1f;
                        }
                        px[y * size + x] = new Color(1, 1, 1, a);
                    }
                tex.SetPixels(px);
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            });
        }
    }

    /// <summary>盾の形（防御アイコン）</summary>
    public static Sprite Shield
    {
        get
        {
            return Cached("shield", () => Shape(64, (x, y) =>
            {
                // 上は角丸の四角、下は尖る
                float w = y > -0.1f ? 0.82f : 0.82f * Mathf.Clamp01((y + 1f) / 0.9f);
                float top = 0.85f;
                float dx = Mathf.Abs(x) - w;
                float dy = y - top;
                return Mathf.Max(dx, dy);
            }));
        }
    }

    // ------------------------------------------------------------
    // 駒
    // ------------------------------------------------------------

    public enum BodyFinish { Wood, Metal, Lacquer }

    /// <summary>レアリティ別の駒本体（尖った先が上。敵駒は180度回転して使う）</summary>
    public static Sprite PieceBody(Rarity rarity)
    {
        bool metallic = rarity == Rarity.Rare || rarity == Rarity.SuperRare;
        bool lacquer = rarity == Rarity.Legend;
        Color body = Palette.RarityBody(rarity);
        BodyFinish finish = lacquer ? BodyFinish.Lacquer : (metallic ? BodyFinish.Metal : BodyFinish.Wood);
        Color edge = lacquer ? Palette.Gold : body * 0.42f;
        return Cached("piece_" + rarity, () => CreatePieceBody(256, body, finish, edge, Palette.Hex(0x8A5BB8)));
    }

    /// <summary>特別な駒の本体（提督＝紺の漆と金、艦娘＝鋼、深海＝深淵）</summary>
    public static Sprite SpecialBody(string kind)
    {
        switch (kind)
        {
            case "naval":
                return Cached("piece_naval", () => CreatePieceBody(256, Palette.Hex(0x1E3358), BodyFinish.Lacquer, Palette.Gold, Palette.Hex(0x4F7FCF)));
            case "steel":
                return Cached("piece_steel", () => CreatePieceBody(256, Palette.Hex(0xBAC4CF), BodyFinish.Metal, Palette.Hex(0x2A3B55), Color.white));
            case "abyss":
                return Cached("piece_abyss", () => CreatePieceBody(256, Palette.Hex(0x151B25), BodyFinish.Lacquer, Palette.Hex(0x3FB8B0), Palette.Hex(0x2F6F78)));
            default:
                return PieceBody(Rarity.Normal);
        }
    }

    /// <summary>錨の紋章（提督・艦娘・物鉄）</summary>
    public static Sprite Anchor
    {
        get
        {
            return Cached("anchor", () => Shape(128, (x, y) =>
            {
                // 上の輪
                float ring = Mathf.Abs(new Vector2(x, y - 0.68f).magnitude - 0.17f) - 0.06f;
                // 軸
                float shank = Box(x, y + 0.05f, 0.065f, 0.62f);
                // 横木
                float stock = Box(x, y - 0.38f, 0.36f, 0.06f);
                // 下の弧（腕）
                float r = new Vector2(x, y + 0.12f).magnitude;
                float arc = Mathf.Abs(r - 0.56f) - 0.065f;
                if (y > -0.12f) arc = Mathf.Max(arc, y + 0.12f);
                // 爪
                float flukeL = new Vector2(x + 0.56f, y + 0.08f).magnitude - 0.11f;
                float flukeR = new Vector2(x - 0.56f, y + 0.08f).magnitude - 0.11f;
                return Mathf.Min(Mathf.Min(Mathf.Min(ring, shank), Mathf.Min(stock, arc)), Mathf.Min(flukeL, flukeR));
            }));
        }
    }

    /// <summary>艦載機のシルエット（上向き）</summary>
    public static Sprite Plane
    {
        get
        {
            return Cached("plane", () => Shape(64, (x, y) =>
            {
                float body = Box(x, y, 0.09f, 0.75f);
                float wing = Box(x, y - 0.12f, 0.8f, 0.11f);
                float tail = Box(x, y + 0.6f, 0.32f, 0.07f);
                return Mathf.Min(body, Mathf.Min(wing, tail));
            }));
        }
    }

    private static float Box(float x, float y, float hw, float hh)
    {
        float dx = Mathf.Abs(x) - hw;
        float dy = Mathf.Abs(y) - hh;
        return new Vector2(Mathf.Max(dx, 0), Mathf.Max(dy, 0)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0);
    }

    /// <summary>駒の落ち影</summary>
    public static Sprite PieceShadow
    {
        get
        {
            return Cached("pieceshadow", () =>
            {
                const int size = 128;
                var tex = NewTexture(size, size, true);
                var px = new Color[size * size];
                float scale = size * 0.5f * 0.88f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float nx = (x + 0.5f - size * 0.5f) / scale;
                        float ny = (y + 0.5f - size * 0.5f) / scale;
                        float d = SignedPolygonDistance(nx, ny) * scale; // px
                        float a = Mathf.Clamp01(0.5f - d / 9f);
                        px[y * size + x] = new Color(0, 0, 0, a * a);
                    }
                tex.SetPixels(px);
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            });
        }
    }

    /// <summary>UI用の駒テクスチャ（UI Toolkitの背景画像に使う）</summary>
    public static Texture2D PieceTexture(Rarity rarity)
    {
        return PieceBody(rarity).texture;
    }

    private static Sprite CreatePieceBody(int size, Color body, BodyFinish finish, Color edgeColor, Color sheenColor)
    {
        var tex = NewTexture(size, size, true);
        tex.filterMode = FilterMode.Trilinear;
        var px = new Color[size * size];

        bool metallic = finish == BodyFinish.Metal;
        bool lacquer = finish == BodyFinish.Lacquer;
        edgeColor.a = 1f;

        float scale = size * 0.5f * 0.94f;           // 1px 余白
        float bevel = size * 0.045f;                  // 面取り幅(px)
        Vector2 light = new Vector2(-0.45f, 0.9f).normalized;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - size * 0.5f) / scale;
                float ny = (y + 0.5f - size * 0.5f) / scale;
                int edge;
                float d = SignedPolygonDistance(nx, ny, out edge) * scale; // px（負=内側）
                if (d > 1f) { px[y * size + x] = Color.clear; continue; }

                Color c = body;

                // 上が明るく下が暗いグラデーション
                c = Shade(c, ny * 0.07f);

                if (lacquer)
                {
                    // 漆：深い艶
                    float sheen = Mathf.Exp(-Mathf.Pow((nx * 0.8f + ny - 0.35f) / 0.25f, 2f));
                    c = Color.Lerp(c, sheenColor, sheen * 0.35f);
                }
                else if (metallic)
                {
                    // 金属：斜めの光沢
                    float sheen = Mathf.Exp(-Mathf.Pow((nx * 0.7f + ny - 0.25f) / 0.22f, 2f));
                    c = Shade(c, sheen * 0.22f - 0.04f);
                }
                else
                {
                    // 木目（縦方向の柾目）
                    float grain = Mathf.Sin(nx * 38f + Mathf.PerlinNoise(nx * 2.2f + 3.1f, ny * 0.7f + 1.7f) * 9f);
                    float mottle = Mathf.PerlinNoise(nx * 3f + 10f, ny * 3f + 4f) - 0.5f;
                    c = Shade(c, grain * 0.025f + mottle * 0.06f);
                }

                // 面取り（縁の向きに応じて明暗）
                float inside = -d;
                if (inside < bevel)
                {
                    Vector2 n = EdgeNormal(edge);
                    float lit = Vector2.Dot(n, light);
                    float t = 1f - Mathf.Clamp01(inside / bevel);
                    c = Shade(c, lit * 0.16f * t);
                }

                // 縁取り線
                float outline = lacquer ? 3.2f : 1.6f;
                if (inside < outline + 1f)
                {
                    float t = Mathf.Clamp01(outline + 1f - inside);
                    c = Color.Lerp(c, edgeColor, t * (lacquer ? 1f : 0.85f));
                }

                c.a = Mathf.Clamp01(0.5f - d);
                px[y * size + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply(true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ------------------------------------------------------------
    // 盤・背景
    // ------------------------------------------------------------

    /// <summary>榧（かや）の柾目風の木目テクスチャ</summary>
    public static Sprite BoardWood
    {
        get
        {
            return Cached("boardwood", () =>
            {
                const int size = 768;
                var tex = NewTexture(size, size, true);
                tex.wrapMode = TextureWrapMode.Clamp;
                var px = new Color[size * size];
                Color baseCol = Palette.BoardWood;
                Color darkCol = Palette.BoardWoodDark;
                for (int y = 0; y < size; y++)
                {
                    float v = (float)y / size;
                    for (int x = 0; x < size; x++)
                    {
                        float u = (float)x / size;
                        float warp = Mathf.PerlinNoise(u * 2.5f + 7.3f, v * 0.8f + 1.1f) * 3.5f
                                   + Mathf.PerlinNoise(u * 9f + 2.0f, v * 2.5f + 5.0f) * 0.6f;
                        float ring = 0.5f + 0.5f * Mathf.Sin((u * 46f + warp) * Mathf.PI);
                        ring = Mathf.Pow(ring, 3f);
                        float fine = Mathf.PerlinNoise(u * 120f, v * 6f) * 0.5f;
                        float mottle = Mathf.PerlinNoise(u * 3f + 20f, v * 3f + 30f);
                        Color c = Color.Lerp(baseCol, darkCol, ring * 0.55f + fine * 0.25f);
                        c = Shade(c, (mottle - 0.5f) * 0.10f);
                        c.a = 1f;
                        px[y * size + x] = c;
                    }
                }
                tex.SetPixels(px);
                tex.Apply(true);
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            });
        }
    }

    /// <summary>画面の背景（中央がほのかに明るい）</summary>
    public static Sprite BackgroundGlow
    {
        get
        {
            return Cached("bgglow", () => Radial(256, (r) =>
            {
                float a = Mathf.Clamp01(1f - r);
                return a * a * (3f - 2f * a);
            }));
        }
    }

    // ------------------------------------------------------------
    // 内部ヘルパー
    // ------------------------------------------------------------

    private static Sprite Cached(string key, System.Func<Sprite> create)
    {
        Sprite s;
        if (cache.TryGetValue(key, out s) && s != null) return s;
        s = create();
        s.name = key;
        s.texture.name = key;
        cache[key] = s;
        return s;
    }

    private static Texture2D NewTexture(int w, int h, bool mipmaps)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipmaps);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.DontSave;
        return tex;
    }

    private static void Fill(Texture2D tex, Color c)
    {
        var px = new Color[tex.width * tex.height];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        tex.SetPixels(px);
        tex.Apply();
    }

    /// <summary>r=0(中心)〜1(縁) に対するアルファで白い円形スプライトを作る</summary>
    private static Sprite Radial(int size, System.Func<float, float> alphaAt)
    {
        var tex = NewTexture(size, size, true);
        var px = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * size + x] = new Color(1, 1, 1, r >= 1f ? 0f : alphaAt(r));
            }
        tex.SetPixels(px);
        tex.Apply(true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>符号付き距離関数（-1..1座標、負=内側）から白い図形を作る</summary>
    private static Sprite Shape(int size, System.Func<float, float, float> sdf)
    {
        var tex = NewTexture(size, size, true);
        var px = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - half) / half;
                float ny = (y + 0.5f - half) / half;
                float d = sdf(nx, ny) * half;
                px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
            }
        tex.SetPixels(px);
        tex.Apply(true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static float RoundRectDistance(float x, float y, float w, float h, float r)
    {
        float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
        float qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
        float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude;
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
    }

    private static Color Shade(Color c, float amount)
    {
        if (amount >= 0) return new Color(c.r + (1 - c.r) * amount, c.g + (1 - c.g) * amount, c.b + (1 - c.b) * amount, c.a);
        float k = 1f + amount;
        return new Color(c.r * k, c.g * k, c.b * k, c.a);
    }

    private static float SignedPolygonDistance(float px, float py)
    {
        int edge;
        return SignedPolygonDistance(px, py, out edge);
    }

    private static float SignedPolygonDistance(float px, float py, out int nearestEdge)
    {
        float minDist = float.MaxValue;
        nearestEdge = 0;
        bool inside = false;
        int n = PieceVerts.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 a = PieceVerts[j], b = PieceVerts[i];
            if ((b.y > py) != (a.y > py) && px < (a.x - b.x) * (py - b.y) / (a.y - b.y) + b.x)
                inside = !inside;

            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(new Vector2(px, py) - a, ab) / ab.sqrMagnitude);
            float dist = (a + ab * t - new Vector2(px, py)).magnitude;
            if (dist < minDist) { minDist = dist; nearestEdge = j; }
        }
        return inside ? -minDist : minDist;
    }

    /// <summary>辺 edge（頂点edge→edge+1）の外向き法線</summary>
    private static Vector2 EdgeNormal(int edge)
    {
        Vector2 a = PieceVerts[edge];
        Vector2 b = PieceVerts[(edge + 1) % PieceVerts.Length];
        Vector2 dir = (b - a).normalized;
        // 頂点は反時計回りなので、外向きは進行方向の右手
        return new Vector2(dir.y, -dir.x);
    }
}
