using UnityEngine;
using System.Collections.Generic;
using TMPro;

/// <summary>盤上のハイライト（移動先・攻撃対象とダメージ予測・選択・直前の手・ホバー）</summary>
public class HighlightManager : MonoBehaviour
{
    public static HighlightManager Instance { get; private set; }

    private readonly List<BoardCell> markedCells = new List<BoardCell>();
    private readonly List<BoardCell> lastMoveCells = new List<BoardCell>();
    private readonly List<TextMeshPro> previewLabels = new List<TextMeshPro>();
    private BoardCell hoverCell;
    private Transform previewRoot;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

    /// <summary>選択した駒の移動先を表示する。攻撃先には予測ダメージを出す</summary>
    public void ShowMoveHighlights(PieceInstance piece, List<MoveValidator.MoveResult> moves)
    {
        BoardManager bm = BoardManager.Instance;
        BoardRenderer renderer = bm.GetRenderer();
        HashSet<Vector2Int> danger = GetAttackedSquares(piece.team == Team.Player ? Team.Enemy : Team.Player);

        foreach (var move in moves)
        {
            BoardCell cell = renderer.GetCell(move.position);
            if (cell == null) continue;

            if (move.isAttack)
            {
                cell.SetMark(CellMark.Attack);
                PieceInstance target = bm.GetPieceAt(move.position);
                if (target != null) ShowDamagePreview(piece, target);
            }
            else
            {
                cell.SetMark(danger.Contains(move.position) ? CellMark.MoveDanger : CellMark.Move);
            }
            markedCells.Add(cell);
        }
    }

    public void ShowSelectedHighlight(Vector2Int pos)
    {
        BoardCell cell = BoardManager.Instance.GetRenderer().GetCell(pos);
        if (cell == null) return;
        cell.SetSelected(true);
        markedCells.Add(cell);
    }

    public void ClearHighlights()
    {
        foreach (var cell in markedCells)
            if (cell != null) cell.ClearSelectionMarks();
        markedCells.Clear();
        foreach (var label in previewLabels)
            if (label != null) label.gameObject.SetActive(false);
    }

    /// <summary>直前の手（移動元と移動先／攻撃先）を淡く示す</summary>
    public void ShowLastMove(Vector2Int from, Vector2Int to)
    {
        foreach (var cell in lastMoveCells)
            if (cell != null) cell.SetLastMove(false);
        lastMoveCells.Clear();

        BoardRenderer renderer = BoardManager.Instance.GetRenderer();
        foreach (var pos in new[] { from, to })
        {
            BoardCell cell = renderer.GetCell(pos);
            if (cell == null) continue;
            cell.SetLastMove(true);
            lastMoveCells.Add(cell);
        }
    }

    public void SetHover(Vector2Int? pos)
    {
        BoardCell cell = pos.HasValue ? BoardManager.Instance.GetRenderer().GetCell(pos.Value) : null;
        if (cell == hoverCell) return;
        if (hoverCell != null) hoverCell.SetHover(false);
        hoverCell = cell;
        if (hoverCell != null) hoverCell.SetHover(true);
    }

    /// <summary>盤を作り直したときに参照を捨てる</summary>
    public void ResetBoard()
    {
        markedCells.Clear();
        lastMoveCells.Clear();
        hoverCell = null;
        foreach (var label in previewLabels)
            if (label != null) label.gameObject.SetActive(false);
    }

    private void ShowDamagePreview(PieceInstance attacker, PieceInstance target)
    {
        string text;
        Color color;
        if (target.data.isTauntPiece)
        {
            text = "無効";
            color = Palette.TextSub;
        }
        else
        {
            int damage = CombatResolver.CalcDamage(attacker, target);
            if (damage >= target.currentHP)
            {
                text = "撃破";
                color = Palette.GoldLight;
            }
            else
            {
                text = "-" + damage;
                color = damage > 0 ? Palette.EnemyLight : Palette.TextSub;
            }
        }

        TextMeshPro label = GetPreviewLabel();
        label.text = text;
        label.color = color;
        label.transform.position = new Vector3(target.boardPosition.x, target.boardPosition.y + 0.02f, 0f);
        label.gameObject.SetActive(true);
    }

    private TextMeshPro GetPreviewLabel()
    {
        foreach (var label in previewLabels)
            if (label != null && !label.gameObject.activeSelf) return label;

        if (previewRoot == null)
        {
            previewRoot = new GameObject("DamagePreview").transform;
            previewRoot.SetParent(transform, false);
        }
        var obj = new GameObject("Preview");
        obj.transform.SetParent(previewRoot, false);
        var tmp = obj.AddComponent<TextMeshPro>();
        tmp.font = GameFonts.NumberTMP;
        tmp.fontSharedMaterial = GameFonts.NumberOutlineMaterial;
        tmp.fontSize = 3.4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(1.2f, 0.6f);
        var mr = obj.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = BoardCell.MarkOrder + 5;
        previewLabels.Add(tmp);
        return tmp;
    }

    /// <summary>指定チームの駒が次の手で攻撃できるマス</summary>
    private static HashSet<Vector2Int> GetAttackedSquares(Team team)
    {
        var positions = new HashSet<Vector2Int>();
        foreach (var piece in BoardManager.Instance.GetTeamPieces(team))
            foreach (var pos in MoveValidator.GetAttackRange(piece))
                positions.Add(pos);
        return positions;
    }
}
