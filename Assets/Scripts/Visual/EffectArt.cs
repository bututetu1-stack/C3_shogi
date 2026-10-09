using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// エフェクト用の画像（Resources/Effects/名前.png）を読み込む。
/// 画像があればそれを使い、なければ呼び出し側の手続き生成スプライトを使う。
/// どの画像も「長い辺 = 1ユニット」の大きさにそろえるので、画像の解像度を気にせず差し替えられる。
/// 透過のない画像は黒を透明として扱うので、黒背景で描いた光や炎もそのまま使える。
/// </summary>
public static class EffectArt
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// <summary>画像があれば返す（なければ null）</summary>
    public static Sprite Get(string name)
    {
        Sprite sprite;
        if (cache.TryGetValue(name, out sprite)) return sprite;

        Texture2D tex = ArtTexture.Load("Effects/" + name, 512);
        if (tex != null)
        {
            ArtTexture.KeyOutBlackIfOpaque(tex);
            float ppu = Mathf.Max(tex.width, tex.height);
            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
            sprite.name = name;
        }
        cache[name] = sprite;
        return sprite;
    }

    public static bool Has(string name)
    {
        return Get(name) != null;
    }

    /// <summary>画像があればそれ、なければ fallback</summary>
    public static Sprite GetOr(string name, Sprite fallback)
    {
        Sprite s = Get(name);
        return s != null ? s : fallback;
    }
}
