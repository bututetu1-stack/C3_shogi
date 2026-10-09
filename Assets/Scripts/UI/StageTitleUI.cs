using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class StageTitleUI : MonoBehaviour
{
    public static StageTitleUI Instance { get; private set; }

    private UIDocument uiDocument;
    private static UnityEngine.TextCore.Text.FontAsset sdfFont;

    private static UnityEngine.TextCore.Text.FontAsset GetSDFFont()
    {
        if (sdfFont == null)
            sdfFont = Resources.Load<UnityEngine.TextCore.Text.FontAsset>("NotoSansJP-SDF");
        return sdfFont;
    }

    private void ApplyFont(VisualElement elem)
    {
        var font = GetSDFFont();
        if (font != null)
            elem.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void EnsureUIDocument()
    {
        if (uiDocument != null) return;

        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            uiDocument = gameObject.AddComponent<UIDocument>();
            // Copy PanelSettings from any existing UIDocument in the scene
            UIDocument[] docs = FindObjectsOfType<UIDocument>();
            for (int i = 0; i < docs.Length; i++)
            {
                if (docs[i] != uiDocument && docs[i].panelSettings != null)
                {
                    uiDocument.panelSettings = docs[i].panelSettings;
                    break;
                }
            }
        }
        uiDocument.sortingOrder = 50; // Above everything else
    }

    public void ShowTitle(int stageNumber, string stageName)
    {
        EnsureUIDocument();
        StopAllCoroutines();
        StartCoroutine(ShowTitleCoroutine(stageNumber, stageName, 2.0f));
    }

    private IEnumerator ShowTitleCoroutine(int stageNumber, string stageName, float duration)
    {
        var root = uiDocument.rootVisualElement;
        root.Clear();
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.pickingMode = PickingMode.Ignore;

        // Semi-transparent dark overlay
        var overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0;
        overlay.style.top = 0;
        overlay.style.right = 0;
        overlay.style.bottom = 0;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.7f);
        overlay.style.justifyContent = Justify.Center;
        overlay.style.alignItems = Align.Center;
        overlay.pickingMode = PickingMode.Ignore;

        // Container for centered text
        var container = new VisualElement();
        container.style.alignItems = Align.Center;
        container.pickingMode = PickingMode.Ignore;

        // Decorative top line
        var topLine = new VisualElement();
        topLine.style.width = 200;
        topLine.style.height = 2;
        topLine.style.backgroundColor = new Color(0.9f, 0.75f, 0.4f, 0.8f);
        topLine.style.marginBottom = 12;
        topLine.pickingMode = PickingMode.Ignore;
        container.Add(topLine);

        // Stage number label
        var stageLabel = new Label("- Stage " + stageNumber + " -");
        stageLabel.style.fontSize = 22;
        stageLabel.style.color = new Color(0.9f, 0.8f, 0.5f);
        stageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        stageLabel.style.marginBottom = 6;
        stageLabel.pickingMode = PickingMode.Ignore;
        ApplyFont(stageLabel);
        container.Add(stageLabel);

        // Stage name label (large, bold)
        var nameLabel = new Label(stageName);
        nameLabel.style.fontSize = 48;
        nameLabel.style.color = Color.white;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameLabel.style.letterSpacing = 8;
        nameLabel.style.marginBottom = 12;
        nameLabel.pickingMode = PickingMode.Ignore;
        ApplyFont(nameLabel);
        container.Add(nameLabel);

        // Decorative bottom line
        var bottomLine = new VisualElement();
        bottomLine.style.width = 200;
        bottomLine.style.height = 2;
        bottomLine.style.backgroundColor = new Color(0.9f, 0.75f, 0.4f, 0.8f);
        bottomLine.pickingMode = PickingMode.Ignore;
        container.Add(bottomLine);

        overlay.Add(container);
        root.Add(overlay);

        // Hold phase, then fade out
        float holdTime = duration * 0.6f;
        float fadeTime = duration * 0.4f;

        yield return new WaitForSeconds(holdTime);

        // Fade out
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;
            float bgAlpha = Mathf.Lerp(0.7f, 0f, t);
            float textAlpha = Mathf.Lerp(1f, 0f, t);
            float lineAlpha = Mathf.Lerp(0.8f, 0f, t);

            overlay.style.backgroundColor = new Color(0f, 0f, 0f, bgAlpha);
            nameLabel.style.color = new Color(1f, 1f, 1f, textAlpha);
            stageLabel.style.color = new Color(0.9f, 0.8f, 0.5f, textAlpha);
            topLine.style.backgroundColor = new Color(0.9f, 0.75f, 0.4f, lineAlpha);
            bottomLine.style.backgroundColor = new Color(0.9f, 0.75f, 0.4f, lineAlpha);
            yield return null;
        }

        root.Clear();
    }
}
