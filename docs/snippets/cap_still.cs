// いまの画面を Builds/Media/stills/NAME.png（1920x1080）に保存する（cap_frames_setup.cs のあと。NAME を置き換えて使う）
var ps = UnityEngine.Object.FindFirstObjectByType<UnityEngine.UIElements.UIDocument>().panelSettings;
var rt = ps.targetTexture;
string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Builds/Media/stills"));
var prev = RenderTexture.active;
RenderTexture.active = rt;
var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
tex.Apply();
RenderTexture.active = prev;
System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "NAME.png"), tex.EncodeToPNG());
UnityEngine.Object.DestroyImmediate(tex);
return "NAME";
