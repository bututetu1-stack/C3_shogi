// コマ撮りの後片付け（RenderTexture を外し、時間の固定を戻す）
var ps = UnityEngine.Object.FindFirstObjectByType<UnityEngine.UIElements.UIDocument>().panelSettings;
var rt = ps.targetTexture;
ps.targetTexture = null;
Camera.main.targetTexture = null;
Time.captureDeltaTime = 0f;
if (rt != null) rt.Release();
return "ok";
