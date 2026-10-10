var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
rt.name = "CaptureRT";
var doc = UnityEngine.Object.FindFirstObjectByType<UnityEngine.UIElements.UIDocument>();
var ps = doc.panelSettings;
ps.targetTexture = rt;
ps.clearColor = false;
Camera.main.targetTexture = rt;
return "ok " + ps.name;

