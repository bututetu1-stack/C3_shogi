using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 盤が左右のUIパネルと重ならず、画面比に関係なく収まるようにカメラを合わせる。
/// 画面サイズが変わったら自動で合わせ直す。
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFitter : MonoBehaviour
{
    public static CameraFitter Instance { get; private set; }

    // UIが占める領域（UIの参照解像度でのpx）
    public const float TopReserve = 84f;
    public const float BottomReserve = 16f;
    public const float LeftReserve = 332f;
    public const float RightReserve = 392f;

    // 盤の周囲に確保する余白（枠・座標表記）
    private const float BoardMargin = 0.8f;

    private Camera cam;
    private int boardSize = 5;
    private int lastWidth;
    private int lastHeight;

    public Vector3 HomePosition { get; private set; }

    public static CameraFitter Ensure()
    {
        if (Instance != null) return Instance;
        Camera main = Camera.main;
        if (main == null) return null;
        var fitter = main.GetComponent<CameraFitter>();
        if (fitter == null) fitter = main.gameObject.AddComponent<CameraFitter>();
        return fitter;
    }

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Palette.Background;
    }

    public void Fit(int size)
    {
        boardSize = size;
        Apply();
    }

    void LateUpdate()
    {
        if (cam.pixelWidth != lastWidth || cam.pixelHeight != lastHeight)
            Apply();
    }

    private void Apply()
    {
        // 描画先（画面またはレンダーテクスチャ）のサイズ
        lastWidth = cam.pixelWidth;
        lastHeight = cam.pixelHeight;
        if (cam == null || lastWidth <= 0 || lastHeight <= 0) return;

        float sw = lastWidth;
        float sh = lastHeight;
        float ui = GetUIScale(sw, sh);

        float left = LeftReserve * ui;
        float right = RightReserve * ui;
        float top = TopReserve * ui;
        float bottom = BottomReserve * ui;
        float world = boardSize + BoardMargin * 2f;

        // 横幅が足りない（縦長画面など）ときはサイドパネルとの重なりを許容して盤を優先
        if ((sw - left - right) < (sh - top - bottom) * 0.6f)
        {
            left = 8f * ui;
            right = 8f * ui;
        }

        float availW = Mathf.Max(1f, sw - left - right);
        float availH = Mathf.Max(1f, sh - top - bottom);
        float ppu = Mathf.Min(availW / world, availH / world);

        cam.orthographicSize = sh / (2f * ppu);

        float boardCenter = (boardSize - 1) * 0.5f;
        float availCenterX = left + availW * 0.5f;
        float availCenterY = bottom + availH * 0.5f;
        HomePosition = new Vector3(
            boardCenter - (availCenterX - sw * 0.5f) / ppu,
            boardCenter - (availCenterY - sh * 0.5f) / ppu,
            -10f);
        transform.position = HomePosition;
    }

    /// <summary>UI Toolkitのパネル拡大率（PanelSettingsの設定から計算）</summary>
    private static float GetUIScale(float sw, float sh)
    {
        UIDocument doc = FindFirstObjectByType<UIDocument>();
        PanelSettings ps = doc != null ? doc.panelSettings : null;
        if (ps == null) return 1f;

        switch (ps.scaleMode)
        {
            case PanelScaleMode.ConstantPixelSize:
                return ps.scale;
            case PanelScaleMode.ScaleWithScreenSize:
                Vector2 refRes = ps.referenceResolution;
                if (refRes.x <= 0 || refRes.y <= 0) return 1f;
                float logW = Mathf.Log(sw / refRes.x, 2f);
                float logH = Mathf.Log(sh / refRes.y, 2f);
                return Mathf.Pow(2f, Mathf.Lerp(logW, logH, ps.match));
            default:
                return 1f;
        }
    }
}
