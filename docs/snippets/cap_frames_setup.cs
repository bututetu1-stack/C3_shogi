// コマ撮りの準備（プレイ中に流す）。1フレームを 1/30 秒に固定し、画面（カメラと UI）を 1920x1080 の RenderTexture に描かせる。
// このあと play_mode_step --count 3 → cap_frame.cs を繰り返す（10コマ/秒）。終わったら cap_frames_teardown.cs
Time.captureDeltaTime = 1f / 30f;
var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
rt.name = "CaptureRT";
var doc = UnityEngine.Object.FindFirstObjectByType<UnityEngine.UIElements.UIDocument>();
doc.panelSettings.targetTexture = rt;
doc.panelSettings.clearColor = false;
Camera.main.targetTexture = rt;
string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Builds/Media"));
System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "frames"));
System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "stills"));
return dir;
