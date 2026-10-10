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
        GetValidMoves(piece, results, true);
    }

    /// <summary>includeInvite=false なら翡翠の「誘うと来てくれる」を含めない（ふだんの動きだけ）</summary>
    public static void GetValidMoves(PieceInstance piece, List<MoveResult> results, bool includeInvite)
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

        // 翡翠: 誘うと来てくれる（味方の部員の隣へ）
        if (includeInvite && piece.data.pieceType == PieceType.Kawasemi && !piece.isSealed) AbilitySystem.AddInviteMoves(piece, results);

        ApplyTaunt(results, bm);
        if (BalanceTuning.KonishikiGrapple) ApplyGrapple(piece, results, bm);
    }

    /// <summary>
    /// 組み止め: 相手の挑発駒（小錦）の隣にいる駒は、その挑発駒の隣から離れる動きができない
    /// （その場から攻撃するか、隣のまま回り込むだけ）。小錦を敵の前線に寄せて足止めするための決まり
    /// </summary>
    private static void ApplyGrapple(PieceInstance piece, List<MoveResult> results, BoardManager bm)
    {
        if (piece.data.pieceType == PieceType.C3) return;
        Vector2Int from = piece.boardPosition;
        PieceInstance holder = null;
        for (int dx = -1; dx <= 1 && holder == null; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                PieceInstance n = bm.GetPieceAt(new Vector2Int(from.x + dx, from.y + dy));
                if (n != null && n.isAlive && n.team != piece.team && n.data.isTauntPiece) { holder = n; break; }
            }
        if (holder == null) return;

        for (int i = results.Count - 1; i >= 0; i--)
        {
            if (results[i].isAttack) continue;
            Vector2Int p = results[i].position;
            if (Mathf.Max(Mathf.Abs(p.x - holder.boardPosition.x), Mathf.Abs(p.y - holder.boardPosition.y)) > 1)
                results.RemoveAt(i);
        }
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
