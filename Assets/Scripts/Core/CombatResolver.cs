using UnityEngine;
using System.Collections;

/// <summary>
/// 駒の移動・攻撃・ダメージ・撃破の処理を一元化する。
/// プレイヤー操作・AI・自動移動（門人）・各種能力は全てここを通す。
/// </summary>
public static class CombatResolver
{
    public static int CalcDamage(PieceInstance attacker, PieceInstance target)
    {
        return Mathf.Max(0, attacker.ATK - target.DEF);
    }

    // ================================================================
    // 1手の実行（攻撃 → 撃破なら前進 → SN消耗 → 成り → 李白）
    // ================================================================
    public static IEnumerator ExecuteMove(PieceInstance piece, MoveValidator.MoveResult move)
    {
        if (piece == null || !piece.isAlive) yield break;

        BoardManager bm = BoardManager.Instance;
        Vector2Int to = move.position;

        if (move.isAttack)
        {
            PieceInstance target = bm.GetPieceAt(to);
            if (target != null && target.team != piece.team)
            {
                bool killed = Attack(piece, target);
                // 撃破できなければ攻撃側はその場に留まる
                if (!killed) yield break;
            }
        }

        // 髑髏の爆発などで攻撃側が倒れた場合はここで終了
        if (!piece.isAlive || !bm.IsEmpty(to)) yield break;

        MovePieceTo(piece, to);
        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayMoveEffect();

        // SN: 移動でHP-1
        if (piece.data.losesHPOnMove)
        {
            piece.currentHP--;
            RefreshHP(piece);
            if (piece.currentHP <= 0)
            {
                Log(Name(piece) + " は力尽きた...");
                KillPiece(piece);
                yield break;
            }
        }

        if (GameManager.Instance != null)
            GameManager.Instance.CheckPromotion(piece);
        if (!piece.isAlive) yield break;

        // 李白の裏返し能力（移動後に発動）
        if (piece.data.pieceType == PieceType.Rihaku && AbilitySystem.Instance != null)
            AbilitySystem.Instance.ExecuteRihakuAbility(piece);
    }

    /// <summary>通常攻撃。撃破したらtrue</summary>
    public static bool Attack(PieceInstance attacker, PieceInstance target)
    {
        if (target.data.isTauntPiece)
        {
            PlayHit(target);
            Log(Name(attacker) + " → " + Name(target) + " ダメージ無効");
            return false;
        }

        int damage = CalcDamage(attacker, target);
        target.currentHP -= damage;
        RefreshHP(target);

        if (target.currentHP <= 0)
        {
            Log(Name(attacker) + " が " + Name(target) + " を撃破！");
            KillPiece(target);
            return true;
        }

        PlayHit(target);
        Log(Name(attacker) + " → " + Name(target) + " " + damage + "ダメージ");
        return false;
    }

    /// <summary>能力によるダメージ。piercing=trueでDEFを無視。撃破したらtrue</summary>
    public static bool ApplyDamage(PieceInstance target, int amount, bool piercing)
    {
        if (target == null || !target.isAlive) return false;

        if (target.data.isTauntPiece)
        {
            PlayHit(target);
            return false;
        }

        int damage = piercing ? amount : Mathf.Max(0, amount - target.DEF);
        target.currentHP -= damage;
        RefreshHP(target);

        if (target.currentHP <= 0)
        {
            KillPiece(target);
            return true;
        }

        PlayHit(target);
        return false;
    }

    /// <summary>
    /// 駒を撃破して盤から除去する（撃破エフェクト・髑髏爆発・リンク消滅を含む）。
    /// checkLinkedDeaths=falseはリンク消滅処理自身から呼ぶとき用。
    /// </summary>
    public static void KillPiece(PieceInstance target, bool checkLinkedDeaths = true)
    {
        if (target == null || !target.isAlive) return;

        BoardManager bm = BoardManager.Instance;
        Vector2Int pos = target.boardPosition;
        int groupId = target.linkedGroupId;

        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayDefeatEffect(pos);

        bm.RemovePieceController(pos);
        bm.RemovePiece(pos);

        if (checkLinkedDeaths && AbilitySystem.Instance != null)
            AbilitySystem.Instance.CheckLinkedDeaths(groupId);
    }

    /// <summary>盤面データと見た目の両方で駒を移動する</summary>
    public static void MovePieceTo(PieceInstance piece, Vector2Int to)
    {
        BoardManager bm = BoardManager.Instance;
        Vector2Int from = piece.boardPosition;
        PieceController pc = bm.GetPieceController(from);
        bm.MovePiece(from, to);
        bm.UpdatePieceControllerPosition(from, to);
        if (pc != null) pc.MoveTo(to);
    }

    public static void RefreshHP(PieceInstance piece)
    {
        PieceController pc = BoardManager.Instance.GetPieceController(piece.boardPosition);
        if (pc != null) pc.UpdateHP();
    }

    public static void RefreshStats(PieceInstance piece)
    {
        PieceController pc = BoardManager.Instance.GetPieceController(piece.boardPosition);
        if (pc != null && pc.GetRenderer() != null) pc.GetRenderer().UpdateAllStats();
    }

    private static void PlayHit(PieceInstance target)
    {
        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayHitEffect(target.boardPosition);
        PieceController pc = BoardManager.Instance.GetPieceController(target.boardPosition);
        if (pc != null) pc.Shake();
    }

    private static string Name(PieceInstance piece)
    {
        return BattleLogUI.ColorName(piece.DisplayName, piece.team);
    }

    private static void Log(string message)
    {
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(message);
    }
}
