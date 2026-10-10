using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 自動プレイ（バランステスト）用の高速モード。
/// Headless の間は駒の見た目・演出・UI を作らず、待ち時間も飛ばして、対局のロジックだけを動かす。
/// </summary>
public static class GameSim
{
    public static bool Headless;

    // ---- 統計（Headless のときだけ集める） ----

    /// <summary>能力によるダメージの出どころ（なこの突撃・艦娘の攻撃など）。能力の実行中だけ入れる</summary>
    public static PieceInstance AbilitySource;
    // 通常攻撃で倒したときの攻撃側
    private static PieceInstance attacker;

    /// <summary>この局で自軍が倒した敵ごとの、倒した自軍の駒の名前（能力以外の巻き込みは「その他」）</summary>
    public static readonly List<string> StageKills = new List<string>();
    /// <summary>StageKills と同じ順の、倒された敵の名前</summary>
    public static readonly List<string> StageKillVictims = new List<string>();
    /// <summary>この局で倒された自軍の駒の名前</summary>
    public static readonly List<string> StageLosses = new List<string>();
    /// <summary>StageLosses と同じ順の、倒した敵の名前（わからなければ「その他」）</summary>
    public static readonly List<string> StageLossKillers = new List<string>();
    /// <summary>この局で成った自軍の駒の名前</summary>
    public static readonly List<string> StagePromotions = new List<string>();
    /// <summary>この局の艦隊の出来事（"着任@手数" "夜戦@着任からの手数" "S@着任からの手数" "A@…" "轟沈@…"）</summary>
    public static readonly List<string> StageFleet = new List<string>();

    public static void BeginStageStats()
    {
        StageKills.Clear();
        StageKillVictims.Clear();
        StageLosses.Clear();
        StageLossKillers.Clear();
        StagePromotions.Clear();
        StageFleet.Clear();
        AbilitySource = null;
        attacker = null;
    }

    public static void SetAttacker(PieceInstance piece) { attacker = piece; }

    /// <summary>能力によるダメージの出どころを一時的に切り替える。戻すときは返り値を EndSource に渡す</summary>
    public static PieceInstance[] BeginSource(PieceInstance source)
    {
        var saved = new[] { attacker, AbilitySource };
        attacker = null;
        AbilitySource = source;
        return saved;
    }

    public static void EndSource(PieceInstance[] saved)
    {
        attacker = saved[0];
        AbilitySource = saved[1];
    }

    /// <summary>駒が倒されたときに CombatResolver から呼ぶ</summary>
    public static void RecordKill(PieceInstance victim)
    {
        if (!Headless || victim == null) return;
        PieceType t = victim.data.pieceType;
        if (t == PieceType.Chuka || t == PieceType.Dopa) return;   // 消える召喚物は数えない
        PieceInstance killer = attacker != null ? attacker : AbilitySource;
        if (victim.team == Team.Player)
        {
            StageLosses.Add(victim.data.displayName);
            StageLossKillers.Add(killer != null && killer.team == Team.Enemy ? killer.data.displayName : "その他");
            return;
        }
        StageKills.Add(killer != null && killer.team == Team.Player ? killer.data.displayName : "その他");
        StageKillVictims.Add(victim.data.displayName);
    }

    /// <summary>自軍の駒が成ったときに呼ぶ</summary>
    public static void RecordPromotion(PieceInstance piece)
    {
        if (Headless && piece != null && piece.team == Team.Player) StagePromotions.Add(piece.data.displayName);
    }

    /// <summary>艦隊の出来事を記録する（Headless のときだけ）</summary>
    public static void RecordFleet(string kind, int moves)
    {
        if (Headless) StageFleet.Add(kind + "@" + moves);
    }

    /// <summary>
    /// コルーチンを入れ子ごと、その場で最後まで回す（WaitForSeconds などの待ちは無視する）。
    /// Headless のときだけ使う。演出の待ちループが混ざると終わらないので上限を設ける
    /// </summary>
    public static void RunSync(IEnumerator routine)
    {
        if (routine == null) return;
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        int steps = 0;
        while (stack.Count > 0)
        {
            if (++steps > 200000)
                throw new System.InvalidOperationException("GameSim.RunSync: コルーチンが終わりません（演出の待ちループが Headless で止まっていない可能性）");
            IEnumerator top = stack.Peek();
            if (!top.MoveNext())
            {
                stack.Pop();
                continue;
            }
            IEnumerator nested = top.Current as IEnumerator;
            if (nested != null) stack.Push(nested);
        }
    }
}
