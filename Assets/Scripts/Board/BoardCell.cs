using UnityEngine;

public enum CellMark
{
    None,
    Move,        // 移動できる（安全）
    MoveDanger,  // 移動できるが敵の効きがある
    Attack       // 攻撃できる敵駒
}

/// <summary>盤の1マス。ハイライト（移動先・攻撃対象・選択・直前の手・ホバー）を表示する</summary>
public class BoardCell : MonoBehaviour
{
    public const int TintOrder = 3;
    public const int MarkOrder = 20;

    public Vector2Int position;

    private SpriteRenderer tint;
    private SpriteRenderer hover;
    private SpriteRenderer mark;
    private SpriteRenderer frame;

    private CellMark currentMark;
    private bool isSelected;
    private bool isLastMove;

    public void Init(Vector2Int pos)
    {
        position = pos;
        tint = CreateLayer("Tint", SpriteFactory.Pixel, TintOrder, 0.97f);
        hover = CreateLayer("Hover", SpriteFactory.Pixel, TintOrder + 1, 0.97f);
        hover.color = Palette.HoverTint;
        mark = CreateLayer("Mark", SpriteFactory.Circle, MarkOrder, 0.26f);
        frame = CreateLayer("Frame", SpriteFactory.CornerBrackets, MarkOrder + 1, 1.02f);
        frame.color = Palette.SelectedFrame;
        ClearAll();
    }

    private SpriteRenderer CreateLayer(string name, Sprite sprite, int order, float scale)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        obj.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.enabled = false;
        return sr;
    }

    public void SetMark(CellMark m)
    {
        currentMark = m;
        switch (m)
        {
            case CellMark.Move:
                mark.sprite = SpriteFactory.Circle;
                mark.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
                mark.color = Palette.MoveDot;
                break;
            case CellMark.MoveDanger:
                mark.sprite = SpriteFactory.Circle;
                mark.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
                mark.color = Palette.DangerDot;
                break;
            case CellMark.Attack:
                mark.sprite = SpriteFactory.CornerBrackets;
                mark.transform.localScale = new Vector3(1.02f, 1.02f, 1f);
                mark.color = Palette.AttackRing;
                break;
        }
        mark.enabled = m != CellMark.None;
    }

    public void SetSelected(bool on)
    {
        isSelected = on;
        frame.enabled = on;
        RefreshTint();
    }

    public void SetLastMove(bool on)
    {
        isLastMove = on;
        RefreshTint();
    }

    public void SetHover(bool on)
    {
        hover.enabled = on;
    }

    public void ClearAll()
    {
        SetMark(CellMark.None);
        isSelected = false;
        frame.enabled = false;
        hover.enabled = false;
        isLastMove = false;
        RefreshTint();
    }

    /// <summary>移動先・攻撃・選択だけを消す（直前の手の表示は残す）</summary>
    public void ClearSelectionMarks()
    {
        SetMark(CellMark.None);
        SetSelected(false);
    }

    private void RefreshTint()
    {
        if (isSelected)
        {
            tint.color = new Color(1f, 0.85f, 0.35f, 0.32f);
            tint.enabled = true;
        }
        else if (isLastMove)
        {
            tint.color = Palette.LastMoveTint;
            tint.enabled = true;
        }
        else
        {
            tint.enabled = false;
        }
    }

    void Update()
    {
        // 攻撃対象と選択枠はゆっくり明滅させる
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
        if (currentMark == CellMark.Attack && mark.enabled)
        {
            Color c = Palette.AttackRing;
            c.a *= pulse;
            mark.color = c;
        }
        if (frame.enabled)
        {
            Color c = Palette.SelectedFrame;
            c.a *= 0.8f + 0.2f * Mathf.Sin(Time.time * 4f);
            frame.color = c;
        }
    }

    public static int GetPromoteRows(int boardSize)
    {
        return boardSize >= 9 ? 3 : 2;
    }
}
