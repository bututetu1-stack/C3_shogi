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
        GetValidMoves(piece, results);
        return results;
    }

    /// <summary>合法手を results に詰める（results は先にクリアされる。AI用にアロケーションを避ける版）</summary>
    public static void GetValidMoves(PieceInstance piece, List<MoveResult> results)
    {
        results.Clear();
        if (piece == null || !piece.isAlive) return;
        if (piece.stunned) return;   // 冷笑されて、この手番は動けない
        if (piece.data.isImmovable) return;
        if (piece.isPromoted && piece.data.isImmovableWhenPromoted) return;

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

        ApplyTaunt(results, bm);
    }

    /// <summary>
    /// 挑発: 攻撃できる位置に相手の挑発駒（小錦）がいるなら、攻撃はその駒にしかできない（移動はできる）
    /// </summary>
    private static void ApplyTaunt(List<MoveResult> results, BoardManager bm)
    {
        bool tauntInRange = false;
        for (int i = 0; i < results.Count; i++)
        {
            if (!results[i].isAttack) continue;
            PieceInstance t = bm.GetPieceAt(results[i].position);
            if (t != null && t.data.isTauntPiece) { tauntInRange = true; break; }
        }
        if (!tauntInRange) return;

        for (int i = results.Count - 1; i >= 0; i--)
        {
            if (!results[i].isAttack) continue;
            PieceInstance t = bm.GetPieceAt(results[i].position);
            if (t == null || !t.data.isTauntPiece) results.RemoveAt(i);
        }
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
