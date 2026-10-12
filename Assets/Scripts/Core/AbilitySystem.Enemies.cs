using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 第9版の敵駒: 巨岩（地形）・傀儡（挑発）・弓兵（射撃）・大盾（正面に強い）・猪突（助走）・妖狐（倒すと狐火）・結界（能力無効）
/// </summary>
public partial class AbilitySystem
{
    private static readonly Color ArrowBrown = new Color(0.95f, 0.78f, 0.45f);
    private static readonly Color FoxBlue = new Color(0.5f, 0.8f, 1f);
    public static readonly Color KekkaiPurple = new Color(0.72f, 0.5f, 1f);

    // ============================================================
    // 結界: 周りの相手の駒の能力を止める
    // ============================================================

    /// <summary>
    /// 能力が止まっているか: けいに封印された、または周り（BalanceTuning.KekkaiRange マス）に相手の結界がいる。
    /// ★・覚醒の数値はそのまま、能力（手番の初め・終わりの能力、攻撃のときの能力、挑発など）だけが止まる
    /// </summary>
    public static bool IsSuppressed(PieceInstance p)
    {
        if (p == null) return false;
        if (p.isSealed) return true;
        return NearKekkai(p);
    }

    /// <summary>周りに相手の結界がいる（封印ではなく、結界で止まっている）</summary>
    public static bool NearKekkai(PieceInstance p)
    {
        if (p == null || p.data.pieceType == PieceType.C3) return false;
        BoardManager bm = BoardManager.Instance;
        if (bm == null) return false;
        int r = BalanceTuning.KekkaiRange;
        for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var pos = new Vector2Int(p.boardPosition.x + dx, p.boardPosition.y + dy);
                if (!bm.IsInBounds(pos)) continue;
                PieceInstance k = bm.GetPieceAt(pos);
                if (k != null && k.isAlive && k.team != p.team && k.data.pieceType == PieceType.Kekkai) return true;
            }
        return false;
    }

    // ============================================================
    // 地形と、数に入れない駒
    // ============================================================

    /// <summary>地形や囮など、勝敗・撃破数・練度に数えない敵（巨岩・傀儡）</summary>
    public static bool IsObstacle(PieceType t)
    {
        return t == PieceType.Kyogan || t == PieceType.Kugutsu;
    }

    // ============================================================
    // 弓兵: 手番の終わりに、前の直線上いちばん近い相手の駒を射る
    // ============================================================
    private IEnumerator ExecuteArchers(Team team)
    {
        BoardManager bm = BoardManager.Instance;
        foreach (var archer in AlivePieces(team, PieceType.Yumihei))
        {
            if (!archer.isAlive || archer.isSealed) continue;
            int dir = team == Team.Enemy ? -1 : 1;   // 敵は下（こちら）を向いている
            PieceInstance target = null;
            for (int y = archer.boardPosition.y + dir; y >= 0 && y < bm.CurrentBoardSize; y += dir)
            {
                PieceInstance p = bm.GetPieceAt(new Vector2Int(archer.boardPosition.x, y));
                if (p == null || IsSubmerged(p)) continue;   // 潜った潜水艦の上は矢が越えていく
                if (p.team != team && p.isAlive && p.data.pieceType != PieceType.C3) target = p;
                break;   // 味方や C3 に当たると、そこで矢は止まる
            }
            if (target == null) continue;
            int damage = CombatResolver.AbilityDamage(archer, BalanceTuning.YumiheiDamage);
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlayArrow(archer.boardPosition, target.boardPosition);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("弓兵", team) + " が " + BattleLogUI.ColorName(target.DisplayName, target.team) + " を射た");
            yield return new WaitForSeconds(0.25f);   // 矢が届いてから
            var source = GameSim.BeginSource(archer);
            CombatResolver.ApplyDamage(target, damage, true);
            GameSim.EndSource(source);
            yield return new WaitForSeconds(0.2f);
        }
    }

    // ============================================================
    // 妖狐: 倒されると、隣に狐火を残す
    // ============================================================
    private void SplitFox(PieceInstance fox, Vector2Int pos)
    {
        BoardManager bm = BoardManager.Instance;
        PieceData fire = bm.GetPieceDataByType(PieceType.Kitsunebi);
        if (fire == null) return;
        var spots = new List<Vector2Int>();
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var p = new Vector2Int(pos.x + dx, pos.y + dy);
                if (bm.IsInBounds(p) && bm.IsEmpty(p)) spots.Add(p);
            }
        int count = Mathf.Min(BalanceTuning.YoukoFoxfires, spots.Count);
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, spots.Count);
            Vector2Int spot = spots[idx];
            spots.RemoveAt(idx);
            PieceController pc = bm.SpawnPiece(fire, fox.team, spot);
            if (pc != null) pc.FlyFrom(new Vector3(pos.x, pos.y, 0f), 0.3f);
            PieceInstance f = bm.GetPieceAt(spot);
            if (f != null && GameManager.Instance != null) GameManager.Instance.ApplySummonBonuses(f);
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlayFoxfire(spot);
        }
        if (count > 0)
        {
            FloatingText.Spawn(pos, "狐火", FoxBlue, 3.2f);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("妖狐", fox.team) + " は倒れ際に狐火を" + count + "つ残した");
        }
    }

    /// <summary>撃破のたびに（CombatResolver.KillPiece から、盤から除く前）: 妖狐の狐火</summary>
    public void OnEnemyPieceKilled(PieceInstance victim, Vector2Int pos)
    {
        if (victim == null || victim.isSealed) return;
        if (victim.data.pieceType == PieceType.Youko) pendingFoxes.Add(new KeyValuePair<PieceInstance, Vector2Int>(victim, pos));
    }

    /// <summary>盤から除いたあとで狐火を出す（倒れた妖狐のマスも空く）</summary>
    public void FlushFoxes()
    {
        if (pendingFoxes.Count == 0) return;
        var list = new List<KeyValuePair<PieceInstance, Vector2Int>>(pendingFoxes);
        pendingFoxes.Clear();
        foreach (var kv in list) SplitFox(kv.Key, kv.Value);
    }

    private readonly List<KeyValuePair<PieceInstance, Vector2Int>> pendingFoxes = new List<KeyValuePair<PieceInstance, Vector2Int>>();
}
