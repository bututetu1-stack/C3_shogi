// 1コマ撮る（cap_frames_setup.cs のあと）。Builds/Media/frames/NNN.rgb に 640x360 の RGB（下の行から）を書き、
// 10コマごとに Builds/Media/stills/frame_NNN.png に 1920x1080 の写真も残す（あとで良い場面を選ぶ）
var ps = UnityEngine.Object.FindFirstObjectByType<UnityEngine.UIElements.UIDocument>().panelSettings;
var rt = ps.targetTexture;
string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Builds/Media"));
int n = System.IO.Directory.GetFiles(System.IO.Path.Combine(dir, "frames"), "*.rgb").Length;
var prev = RenderTexture.active;
if (n % 10 == 0)
{
    RenderTexture.active = rt;
    var full = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
    full.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
    full.Apply();
    System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "stills", "frame_" + n.ToString("D3") + ".png"), full.EncodeToPNG());
    UnityEngine.Object.DestroyImmediate(full);
}
// 2段で縮める（1920→1280→640）と文字のちらつきが少ない
var mid = RenderTexture.GetTemporary(1280, 720, 0, RenderTextureFormat.ARGB32);
var small = RenderTexture.GetTemporary(640, 360, 0, RenderTextureFormat.ARGB32);
mid.filterMode = FilterMode.Bilinear;
Graphics.Blit(rt, mid);
Graphics.Blit(mid, small);
RenderTexture.active = small;
var tex = new Texture2D(640, 360, TextureFormat.RGB24, false);
tex.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
tex.Apply();
RenderTexture.active = prev;
RenderTexture.ReleaseTemporary(mid);
RenderTexture.ReleaseTemporary(small);
System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "frames", n.ToString("D3") + ".rgb"), tex.GetRawTextureData());
UnityEngine.Object.DestroyImmediate(tex);
return n;
