using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resources の画像を、縮小した読み書きできるテクスチャとして読み込む。
/// インポート設定で「読み書き可能」にしなくても使えるので、画像は置くだけでよい。
/// </summary>
public static class ArtTexture
{
    /// <summary>長い辺が maxSize 以下になるように縮めて読み込む（なければ null）</summary>
    public static Texture2D Load(string path, int maxSize)
    {
        Texture2D src = Resources.Load<Texture2D>(path);
        if (src == null) return null;

        float scale = Mathf.Min(1f, (float)maxSize / Mathf.Max(src.width, src.height));
        int w = Mathf.Max(1, Mathf.RoundToInt(src.width * scale));
        int h = Mathf.Max(1, Mathf.RoundToInt(src.height * scale));

        // 半分ずつ縮めて、細かい模様のちらつきを抑える
        var temps = new List<RenderTexture>();
        Texture from = src;
        int cw = src.width, ch = src.height;
        while (cw > w * 2)
        {
            cw = Mathf.Max(w, cw / 2);
            ch = Mathf.Max(h, ch / 2);
            RenderTexture step = Temporary(cw, ch);
            Graphics.Blit(from, step);
            temps.Add(step);
            from = step;
        }
        RenderTexture dst = Temporary(w, h);
        Graphics.Blit(from, dst);
        temps.Add(dst);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = dst;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = prev;
        foreach (RenderTexture rt in temps) RenderTexture.ReleaseTemporary(rt);
        Resources.UnloadAsset(src);

        tex.name = path;
        tex.filterMode = FilterMode.Trilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.DontSave;
        tex.Apply(true);
        return tex;
    }

    /// <summary>
    /// 透過のない画像（真っ黒な背景に描いた光・炎・稲妻など）なら、黒を透明に変える。
    /// 明るいところほど不透明になるので、黒背景で描かれたエフェクトがそのまま使える。
    /// </summary>
    public static void KeyOutBlackIfOpaque(Texture2D tex)
    {
        Color[] px = tex.GetPixels();
        int seeThrough = 0;
        for (int i = 0; i < px.Length; i++)
            if (px[i].a < 0.9f) seeThrough++;
        if (seeThrough > px.Length / 1000) return;   // もともと透過している

        for (int i = 0; i < px.Length; i++)
        {
            Color c = px[i];
            float a = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            px[i] = a < 0.02f ? Color.clear : new Color(c.r / a, c.g / a, c.b / a, a);
        }
        tex.SetPixels(px);
        tex.Apply(true);
    }

    private static RenderTexture Temporary(int w, int h)
    {
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.filterMode = FilterMode.Bilinear;
        return rt;
    }
}
