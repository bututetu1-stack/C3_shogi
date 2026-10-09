using UnityEngine;
using System.Collections.Generic;

public class HighlightManager : MonoBehaviour
{
    public static HighlightManager Instance { get; private set; }

    private List<BoardCell> highlightedCells = new List<BoardCell>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

    /// <summary>移動先をハイライトする（既存のハイライトは呼び出し側で消すこと）</summary>
    public void ShowMoveHighlights(List<MoveValidator.MoveResult> moves)
    {
        BoardRenderer renderer = BoardManager.Instance.GetRenderer();

        // 敵の攻撃範囲を計算
        var enemyAttackPositions = GetEnemyAttackPositions();

        foreach (var move in moves)
        {
            BoardCell cell = renderer.GetCell(move.position);
            if (cell != null)
            {
                // 敵の効き範囲内なら赤（攻撃先でも非攻撃先でも）
                if (enemyAttackPositions.Contains(move.position))
                    cell.SetHighlight(BoardCell.HighlightAttack);
                else
                    cell.SetHighlight(BoardCell.HighlightMove);
                highlightedCells.Add(cell);
            }
        }
    }

    public void ShowSelectedHighlight(Vector2Int pos)
    {
        BoardRenderer renderer = BoardManager.Instance.GetRenderer();
        BoardCell cell = renderer.GetCell(pos);
        if (cell != null)
        {
            cell.SetHighlight(BoardCell.HighlightSelected);
            highlightedCells.Add(cell);
        }
    }

    public void ClearHighlights()
    {
        foreach (var cell in highlightedCells)
        {
            if (cell != null) cell.ResetColor();
        }
        highlightedCells.Clear();
    }

    // 全敵駒の攻撃範囲を収集（味方駒位置も含む = 真の効き範囲）
    private HashSet<Vector2Int> GetEnemyAttackPositions()
    {
        var positions = new HashSet<Vector2Int>();
        BoardManager bm = BoardManager.Instance;
        var enemyPieces = bm.GetTeamPieces(Team.Enemy);

        foreach (var enemy in enemyPieces)
        {
            var range = MoveValidator.GetAttackRange(enemy);
            foreach (var pos in range)
                positions.Add(pos);
        }
        return positions;
    }
}
