using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 日本語の改行ルール（禁則・文節）に合わせて自動で改行するラベル。
/// 実際の幅が決まったときに文字幅を測って改行位置を入れ直す。
/// </summary>
public class WrappedLabel : Label
{
    private string rawText;
    private float wrappedWidth = -1f;

    public WrappedLabel(string text)
    {
        rawText = text ?? "";
        this.text = rawText;
        style.whiteSpace = WhiteSpace.Normal;
        pickingMode = PickingMode.Ignore;
        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    /// <summary>改行前の元の文章</summary>
    public string RawText
    {
        get { return rawText; }
        set
        {
            rawText = value ?? "";
            wrappedWidth = -1f;
            text = rawText;
        }
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        float width = contentRect.width;
        if (width <= 1f || Mathf.Abs(width - wrappedWidth) < 0.5f) return;
        wrappedWidth = width;

        // 測定の誤差でUI側が勝手に折り返さないよう、少し余裕を持たせる
        // （スクロールバーが出て幅が少し縮んだときなどに、最後の1文字だけ次の行へ落ちるのを防ぐ）
        string wrapped = JapaneseLineBreaker.Wrap(rawText, width - Mathf.Max(8f, width * 0.035f), Measure);
        if (wrapped != text) text = wrapped;
    }

    private float Measure(string line)
    {
        return MeasureTextSize(line, 0f, MeasureMode.Undefined, 0f, MeasureMode.Undefined).x;
    }
}
