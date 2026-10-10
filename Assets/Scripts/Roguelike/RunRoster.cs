using System.Collections.Generic;
using UnityEngine;

/// <summary>周のあいだ部員ごとに持つ成長（練度と★）</summary>
[System.Serializable]
public class RunMember
{
    public PieceType type;
    public Rarity rarity;
    public int xp;              // 練度
    public int stars;           // ★（0〜3）
    public int kills;           // この周で倒した数
    public int featXpThisStage; // この局の活躍で得た練度（上限あり）
}

/// <summary>
/// 部員の練度と★。周のあいだ残る（局ごとに駒は置き直すが、練度は持ち越す）。
/// 練度は「敵を倒す」「局を生き残る」「能力が決まる（活躍、1局に上限あり）」「鍛える札」でたまり、
/// レアリティごとの閾値で★が上がる（ブロンズは早く、激レアは遅く育つ）。★の効果は BalanceTuning。
/// </summary>
public class RunRoster
{
    private readonly List<RunMember> members = new List<RunMember>();

    public IList<RunMember> Members { get { return members.AsReadOnly(); } }

    public void Clear() { members.Clear(); }

    /// <summary>中断データから戻す</summary>
    public void Restore(RunMember[] saved)
    {
        members.Clear();
        if (saved != null) members.AddRange(saved);
    }

    /// <summary>部員を名簿に加える（もういれば何もしない）</summary>
    public RunMember Add(PieceData data)
    {
        if (data == null) return null;
        RunMember m = Get(data.pieceType);
        if (m != null) return m;
        m = new RunMember { type = data.pieceType, rarity = data.rarity };
        members.Add(m);
        return m;
    }

    public RunMember Get(PieceType type)
    {
        foreach (var m in members) if (m.type == type) return m;
        return null;
    }

    public int StarsOf(PieceType type)
    {
        RunMember m = Get(type);
        return m != null ? m.stars : 0;
    }

    /// <summary>★ごとに必要な練度（★1・★2・★3）</summary>
    public static int[] Thresholds(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Bronze: return BalanceTuning.StarXpBronze;
            case Rarity.SuperRare:
            case Rarity.Legend: return BalanceTuning.StarXpSuperRare;
            default: return BalanceTuning.StarXpRare;
        }
    }

    public static int StarsFor(Rarity rarity, int xp)
    {
        int[] t = Thresholds(rarity);
        int stars = 0;
        while (stars < t.Length && xp >= t[stars]) stars++;
        return stars;
    }

    /// <summary>練度の表示（"6/10（あと4）"、★3なら "16（★3）"）</summary>
    public static string XpText(RunMember m)
    {
        int next = NextThreshold(m);
        return next > 0 ? m.xp + "/" + next + "（あと" + (next - m.xp) + "）" : m.xp + "（★3）";
    }

    /// <summary>次の★に届く練度（★3なら -1）</summary>
    public static int NextThreshold(RunMember m)
    {
        int[] t = Thresholds(m.rarity);
        return m.stars < t.Length ? t[m.stars] : -1;
    }

    /// <summary>新しい局の始め（活躍の上限を数え直す）</summary>
    public void BeginStage()
    {
        foreach (var m in members) m.featXpThisStage = 0;
    }

    /// <summary>練度を足す。★が上がったら、盤上のその駒にすぐ効果を付けて知らせる（piece は null でもよい）</summary>
    public void AddXp(RunMember m, int amount, PieceInstance piece = null)
    {
        if (m == null || amount <= 0) return;
        int before = m.stars;
        m.xp += amount;
        m.stars = StarsFor(m.rarity, m.xp);
        if (m.stars > before && piece != null && piece.isAlive)
        {
            ApplyStars(piece, before, m.stars);
            if (BoardManager.Instance == null) return;   // 盤のない場面（テストなど）は数値だけ
            CombatResolver.RefreshStats(piece);
            FloatingText.Spawn(piece.boardPosition, "★" + m.stars, Palette.GoldLight, 3.8f, 0.2f);
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlayPromoteEffect(piece.boardPosition);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(piece.DisplayName, piece.team) + " の練度が上がった（★" + m.stars + "：" + StarEffectText(m.stars) + "）");
        }
    }

    /// <summary>★ from から to までの効果を駒に付ける（★1 攻撃・★2 体力・★3 防御）</summary>
    public static void ApplyStars(PieceInstance p, int from, int to)
    {
        for (int s = from + 1; s <= to; s++)
        {
            if (s == 1) p.bonusATK += BalanceTuning.StarATK;
            else if (s == 2) p.AddMaxHP(BalanceTuning.StarHP);
            else if (s == 3) p.bonusDEF += BalanceTuning.StarDEF;
        }
    }

    /// <summary>★の効果の説明（★1〜★3）</summary>
    public static string StarEffectText(int star)
    {
        switch (star)
        {
            case 1: return "攻撃+" + BalanceTuning.StarATK;
            case 2: return "体力+" + BalanceTuning.StarHP;
            case 3: return "防御+" + BalanceTuning.StarDEF + "・覚醒の解禁";
            default: return "";
        }
    }

    // ------------------------------------------------------------
    // 練度のたまり方（GameManager.Instance.Roster を通して呼ぶ）
    // ------------------------------------------------------------

    /// <summary>自軍の部員が敵を倒した</summary>
    public void OnKill(PieceInstance killer, PieceInstance victim)
    {
        if (killer == null || victim == null || killer.team != Team.Player || victim.team != Team.Enemy) return;
        PieceType v = victim.data.pieceType;
        if (v == PieceType.Chuka || v == PieceType.Dopa) return;
        RunMember m = Get(killer.data.pieceType);
        if (m == null) return;
        m.kills++;
        AddXp(m, BalanceTuning.XpPerKill, killer);
        CountActivity(m, killer);
    }

    /// <summary>部員の能力が決まった（活躍）。1局に BalanceTuning.FeatXpMaxPerStage まで練度になる</summary>
    public void OnFeat(PieceInstance piece)
    {
        if (piece == null || piece.team != Team.Player) return;
        RunMember m = Get(piece.data.pieceType);
        if (m == null) return;
        if (m.featXpThisStage < BalanceTuning.FeatXpMaxPerStage)
        {
            m.featXpThisStage += BalanceTuning.XpPerFeat;
            AddXp(m, BalanceTuning.XpPerFeat, piece);
        }
        CountActivity(m, piece);
    }

    /// <summary>覚醒の条件: ★3の部員が、その局で活躍を重ねる（練度の上限とは別に数える）</summary>
    private static void CountActivity(RunMember m, PieceInstance piece)
    {
        if (piece == null || !piece.isAlive || piece.awakened || !piece.data.canAwaken || m.stars < 3) return;
        piece.awakenCharge++;
        if (piece.awakenCharge < BalanceTuning.AwakenActivities) return;
        if (GameManager.Instance != null) GameManager.Instance.Awaken(piece);
        else Awakening.Apply(piece);
    }

    /// <summary>局に勝った: 盤に残っている部員の練度を上げる</summary>
    public void OnStageWon(List<PieceInstance> playerPieces)
    {
        foreach (var p in playerPieces)
        {
            if (!p.isAlive) continue;
            RunMember m = Get(p.data.pieceType);
            if (m != null) AddXp(m, BalanceTuning.XpPerStageSurvived, p);
        }
    }

    /// <summary>能力が決まったことを名簿に伝える（ゲームが動いていないときは何もしない）</summary>
    public static void Feat(PieceInstance piece)
    {
        if (GameManager.Instance != null && GameManager.Instance.Roster != null) GameManager.Instance.Roster.OnFeat(piece);
    }

    /// <summary>自動プレイの記録用（"なこ:★2:9"）</summary>
    public string[] Summary(System.Func<PieceType, string> nameOf)
    {
        var list = new List<string>();
        foreach (var m in members) list.Add(nameOf(m.type) + ":★" + m.stars + ":" + m.xp);
        return list.ToArray();
    }
}
