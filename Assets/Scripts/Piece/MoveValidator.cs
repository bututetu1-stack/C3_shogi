using UnityEngine;
using System.Collections.Generic;

public static class MoveValidator
{
    public struct MoveResult
    {
        public Vector2Int position;
        public bool isAttack;
    }

    public static List<MoveResult> GetValidMoves(PieceInstance piece)
    {
        var results = new List<MoveResult>();
        if (piece == null || !piece.isAlive) return results;
        if (piece.data.isImmovable) return results;
        if (piece.isPromoted && piece.data.isImmovableWhenPromoted) return results;

        MoveDirection[] directions = piece.GetMoveDirections();
        BoardManager bm = BoardManager.Instance;

        foreach (var dir in directions)
        {
            for (int dist = 1; dist <= dir.maxDistance; dist++)
            {
                Vector2Int target = piece.boardPosition + dir.direction * dist;

                if (!bm.IsInBounds(target)) break;

                PieceInstance targetPiece = bm.GetPieceAt(target);

                if (targetPiece == null)
                {
                    results.Add(new MoveResult { position = target, isAttack = false });
                }
                else if (targetPiece.team != piece.team)
                {
                    results.Add(new MoveResult { position = target, isAttack = true });
                    if (!dir.canJump) break;
                }
                else
                {
                    if (!dir.canJump) break;
                }

                if (dir.canJump && dist >= dir.maxDistance) break;
                if (!dir.canJump && targetPiece != null) break;
            }
        }

        return results;
    }

    // 駒の攻撃範囲を取得（味方駒の位置も含む = 防衛範囲）
    public static HashSet<Vector2Int> GetAttackRange(PieceInstance piece)
    {
        var positions = new HashSet<Vector2Int>();
        if (piece == null || !piece.isAlive) return positions;
        if (piece.isPromoted && piece.data.isImmovableWhenPromoted) return positions;

        MoveDirection[] directions = piece.GetMoveDirections();
        BoardManager bm = BoardManager.Instance;

        foreach (var dir in directions)
        {
            for (int dist = 1; dist <= dir.maxDistance; dist++)
            {
                Vector2Int target = piece.boardPosition + dir.direction * dist;
                if (!bm.IsInBounds(target)) break;

                positions.Add(target); // 味方位置も含める

                PieceInstance targetPiece = bm.GetPieceAt(target);
                if (targetPiece != null && !dir.canJump) break;
                if (dir.canJump && dist >= dir.maxDistance) break;
            }
        }
        return positions;
    }
}
