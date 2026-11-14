using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// PlayStation 鯖の部員の能力: RN（ブロック・3本同時プレイ）、寝癖（デイリー消化・えちち連合・外跳ね）、無糖（死んで覚える・スタッガー・篝火）。
/// えちち連合（会長 寝癖・副会長 物鉄、会員 小錦・RN・翡翠・ヲツ・無糖・僕）もここ
/// </summary>
public partial class AbilitySystem
{
    private static readonly Color BlockSteel = new Color(0.62f, 0.78f, 1f);
    private static readonly Color DailyPink = new Color(1f, 0.6f, 0.82f);
    private static readonly Color EmberOrange = new Color(1f, 0.55f, 0.2f);
    private static readonly Color DiedRed = new Color(0.9f, 0.18f, 0.14f);

    // ============================================================
    // 駒の情報に出す状態（ブロック・デイリー・死んで覚えた量・スタッガー）
    // ============================================================
    private static string GamerStatusLine(PieceInstance p)
    {
        switch (p.data.pieceType)
        {
            case PieceType.RN:
                return p.block > 0
                    ? "ブロック " + p.block + "（防御とは別に、受けるダメージをあと" + p.block + "防ぐ。次の自分の手番の初めに消える）"
                    : "ブロック 0（自分の手番の終わりに" + RnBlockOf(p) + "を得る）";
            case PieceType.Negu:
            {
                string s = "デイリー " + p.dailyCount + "/" + NeguEveryOf(p) + "（報酬 攻撃+" + p.dailyRewards + "/" + NeguCapOf(p) + "）";
                if (p.allianceATK > 0) s += "　えちち連合 攻撃+" + p.allianceATK;
                return s;
            }
            case PieceType.Mutou:
            {
                int learned = GameManager.Instance != null && p.team == Team.Player ? GameManager.Instance.Roster.DeathBonus(PieceType.Mutou) : 0;
                string s = "死んで覚えた 攻撃+" + learned + "/" + BalanceTuning.MutouDeathCap;
                if (p.awakened) s += p.bonfireUsed ? "（篝火は使った）" : "（篝火あり）";
                return s;
            }
            default:
            {
                var parts = new List<string>();
                if (p.posture > 0) parts.Add("体幹 " + p.posture + "/" + PostureMax(p));
                if (p.staggered) parts.Add("スタッガー（次に受けるダメージ+" + BalanceTuning.MutouStaggerDamage + "）");
                if (p.block > 0) parts.Add("ブロック " + p.block + "（受けるダメージをあと" + p.block + "防ぐ。次の自分の手番の初めに消える）");
                return parts.Count > 0 ? string.Join("　", parts) : null;
            }
        }
    }

    /// <summary>部員の集まり（えちち連合の同志・運営会議・鯖缶の管理者権限）が効いていれば、状態の行に足す</summary>
    private static string WithGroups(PieceInstance p, string s)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(s)) parts.Add(s);
        int c = ComradeDEF(p);
        if (c > 0) parts.Add("えちち連合の同志 防御+" + c);
        if (p.staffATK > 0) parts.Add("運営会議 攻撃+" + p.staffATK);
        if ((p.isSealed || NearKekkai(p)) && AdminGuarded(p)) parts.Add("管理者権限で能力無効を防いでいる");
        return parts.Count > 0 ? string.Join("　", parts) : null;
    }

    private static int RnBlockOf(PieceInstance p)
    {
        int b = p.isPromoted ? BalanceTuning.RnPromotedBlock : BalanceTuning.RnBlock;
        return b + (p.awakened ? BalanceTuning.RnAwakenedBlockBonus : 0);
    }

    /// <summary>3本同時プレイ（ポケモンスリープ・SEKIRO の弾き）が動くか: 成っているか、覚醒している</summary>
    private static bool RnMultiPlay(PieceInstance p) { return (p.isPromoted || p.awakened) && !IsSuppressed(p); }

    /// <summary>寝癖のデイリーが報酬になる手番の数（外跳は短く、覚醒は毎手番）と、報酬の上限</summary>
    private static int NeguEveryOf(PieceInstance p) { return p.awakened ? 1 : p.isPromoted ? BalanceTuning.NeguPromotedDailyEvery : BalanceTuning.NeguDailyEvery; }
    private static int NeguCapOf(PieceInstance p) { return p.awakened ? BalanceTuning.NeguAwakenedDailyCap : BalanceTuning.NeguDailyCap; }

    // ============================================================
    // 手番の初めと終わり
    // ============================================================

    /// <summary>手番の初め: ブロックはこちらの手番が来たら消える（STS2 と同じ）。動いたかどうかを数え直す。えちち連合を確かめる</summary>
    private void BeginGamerTurn(Team team)
    {
        foreach (var p in BoardManager.Instance.GetTeamPieces(team))
        {
            p.block = 0;
            p.acted = false;
        }
        RefreshAlliance(team);
        RefreshStaffMeeting(team);
    }

    // ============================================================
    // えちち連合
    // ============================================================

    /// <summary>えちち連合の部員か（会長 寝癖・副会長 物鉄（提督・物鉄・改も）、会員 小錦・RN・翡翠・ヲツ・無糖・僕）</summary>
    public static bool IsAllianceMember(PieceType t)
    {
        switch (t)
        {
            case PieceType.Negu:
            case PieceType.Monotetsu:
            case PieceType.Konishiki:
            case PieceType.RN:
            case PieceType.Kawasemi:
            case PieceType.Wotsu:
            case PieceType.Mutou:
            case PieceType.Boku:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 同志: 連合の部員は、周り1マスに同じ陣営の仲間がいると防御が上がる。
    /// 盤の上の並びで決まるので、その場で数える（AI の読みで駒を動かしたときもそのまま効く）。その駒自身の能力なので、能力無効のあいだは止まる
    /// </summary>
    public static int ComradeDEF(PieceInstance p)
    {
        if (p == null || !p.isAlive || BalanceTuning.AllianceComradeDEF == 0 || !IsAllianceMember(p.data.pieceType)) return 0;
        BoardManager bm = BoardManager.Instance;
        if (bm == null || bm.GetPieceAt(p.boardPosition) != p) return 0;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                PieceInstance q = bm.GetPieceAt(p.boardPosition + new Vector2Int(dx, dy));
                if (q != null && q.isAlive && q.team == p.team && IsAllianceMember(q.data.pieceType))
                    return IsSuppressed(p) ? 0 : BalanceTuning.AllianceComradeDEF;
            }
        return 0;
    }

    // ============================================================
    // 運営（物鉄・なこ・光春・李白）と鯖缶（鶏炭・ヲツ・僕）
    // ============================================================

    /// <summary>部の運営の部員か（物鉄は提督・物鉄・改も）</summary>
    public static bool IsStaffMember(PieceType t)
    {
        return t == PieceType.Monotetsu || t == PieceType.Nako || t == PieceType.Mitsuharu || t == PieceType.Rihaku;
    }

    /// <summary>鯖缶（サーバー管理者）の部員か</summary>
    public static bool IsSabakan(PieceType t)
    {
        return t == PieceType.Kei || t == PieceType.Wotsu || t == PieceType.Boku;
    }

    /// <summary>
    /// 管理者権限: 鯖缶の部員と、その周り（SabakanAdminRange マス）の味方は、能力無効（結界・封印）にかからない。
    /// 鯖缶自身も止まらないので、権限はいつも効く
    /// </summary>
    public static bool AdminGuarded(PieceInstance p)
    {
        int range = BalanceTuning.SabakanAdminRange;
        if (p == null || !p.isAlive || range < 0) return false;
        if (IsSabakan(p.data.pieceType)) return true;
        BoardManager bm = BoardManager.Instance;
        if (bm == null || bm.GetPieceAt(p.boardPosition) != p) return false;
        for (int dx = -range; dx <= range; dx++)
            for (int dy = -range; dy <= range; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                PieceInstance q = bm.GetPieceAt(p.boardPosition + new Vector2Int(dx, dy));
                if (q != null && q.isAlive && q.team == p.team && IsSabakan(q.data.pieceType)) return true;
            }
        return false;
    }

    /// <summary>運営会議: 盤に運営の部員が2人以上いるあいだ、運営の部員はみな攻撃が上がる（能力無効のあいだは止まる）</summary>
    private void RefreshStaffMeeting(Team team)
    {
        var staff = new List<PieceInstance>();
        foreach (var p in BoardManager.Instance.GetTeamPieces(team))
            if (p.isAlive && IsStaffMember(p.data.pieceType)) staff.Add(p);
        bool meeting = staff.Count >= BalanceTuning.StaffMeetingMin;
        bool changed = false;
        foreach (var s in staff)
        {
            int atk = meeting && !IsSuppressed(s) ? BalanceTuning.StaffMeetingATK : 0;
            if (atk == s.staffATK) continue;
            changed = changed || (atk > 0) != (s.staffATK > 0);
            s.bonusATK += atk - s.staffATK;
            s.staffATK = atk;
            CombatResolver.RefreshStats(s);
        }
        if (changed && BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(meeting
                ? "運営会議（運営の部員" + staff.Count + "人、攻撃+" + BalanceTuning.StaffMeetingATK + "）"
                : "運営会議はお開きになった");
    }

    /// <summary>会長: 盤にいる連合の仲間1人につき、寝癖の攻撃が上がる（副会長の物鉄は2人分。上限あり）</summary>
    private void RefreshAlliance(Team team)
    {
        var negus = AlivePieces(team, PieceType.Negu);
        if (negus.Count == 0) return;
        int members = 0;
        foreach (var p in BoardManager.Instance.GetTeamPieces(team))
            if (p.isAlive && p.data.pieceType != PieceType.Negu && IsAllianceMember(p.data.pieceType))
                members += p.data.pieceType == PieceType.Monotetsu ? 2 : 1;
        foreach (var n in negus)
        {
            int atk = IsSuppressed(n) ? 0 : Mathf.Min(BalanceTuning.NeguAllianceCap, members * BalanceTuning.NeguAllianceATK);
            if (atk == n.allianceATK) continue;
            n.bonusATK += atk - n.allianceATK;
            n.allianceATK = atk;
            CombatResolver.RefreshStats(n);
            FloatingText.Spawn(n.boardPosition, atk > 0 ? "えちち連合 攻撃+" + atk : "連合解散", DailyPink, 3f, 0.2f);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(n.DisplayName, n.team)
                    + (atk > 0 ? " が会長のえちち連合（攻撃+" + atk + "）" : " のえちち連合は、仲間がいなくなって一度解散した"));
        }
    }

    /// <summary>手番の終わり: RN のブロックとポケモンスリープ、寝癖のデイリー消化</summary>
    private IEnumerator ExecuteGamers(Team team)
    {
        RefreshAlliance(team);
        RefreshStaffMeeting(team);

        foreach (var rn in AlivePieces(team, PieceType.RN))
        {
            if (IsSuppressed(rn)) continue;
            // ポケモンスリープ: 動かなかった手番は、寝ていたことにして回復する
            if (RnMultiPlay(rn) && !rn.acted && rn.currentHP < rn.MaxHP)
            {
                CombatResolver.Heal(rn, BalanceTuning.RnSleepHeal);
                FloatingText.Spawn(rn.boardPosition, "睡眠リサーチ", BlockSteel, 2.8f, 0.35f);
            }
            // STS2: ブロックを得る（次の自分の手番の初めまで）
            rn.block = Mathf.Max(rn.block, RnBlockOf(rn));
            FloatingText.Spawn(rn.boardPosition, "ブロック" + rn.block, BlockSteel, 2.6f);
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlayGuard(rn.boardPosition);
        }

        foreach (var n in AlivePieces(team, PieceType.Negu))
        {
            if (IsSuppressed(n) || n.dailyRewards >= NeguCapOf(n)) continue;
            n.dailyCount++;
            if (n.dailyCount < NeguEveryOf(n))
            {
                FloatingText.Spawn(n.boardPosition, "デイリー " + n.dailyCount + "/" + NeguEveryOf(n), DailyPink, 2.4f);
                continue;
            }
            n.dailyCount = 0;
            n.dailyRewards++;
            n.bonusATK += 1;
            CombatResolver.RefreshStats(n);
            FloatingText.Spawn(n.boardPosition, "デイリー報酬 攻+1", DailyPink, 3f);
            if (BattleEffects.Instance != null)
            {
                BattleEffects.Instance.PlayBuffEffect(n.boardPosition, true);
                if (n.awakened) BattleEffects.Instance.PlayAwakenBurst("Gacha", n.boardPosition, n.data.awakenColor, true);
            }
            SpeechBubble.SayMaybe(n, PieceLines.NeguLines, 0.5f);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(n.DisplayName, n.team) + " はデイリーを消化した（攻撃+1、この局で+" + n.dailyRewards + "）");
            RunRoster.Feat(n);
            yield return new WaitForSeconds(0.25f);
        }
    }

    // ============================================================
    // RN: ブロックで防いだとき、SEKIRO の弾き
    // ============================================================

    /// <summary>ブロックで absorbed だけ防いだ（CombatResolver から）</summary>
    public void OnBlocked(PieceInstance target, int absorbed)
    {
        FloatingText.Spawn(target.boardPosition, "ブロック-" + absorbed, BlockSteel, 3f, 0.15f);
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayGuard(target.boardPosition);
        if (target.data.pieceType != PieceType.RN) return;
        SpeechBubble.SayMaybe(target, PieceLines.RnLines, 0.35f);
        RunRoster.Feat(target);
    }

    /// <summary>体幹の上限（敵の最大体力。強い敵ほど崩しにくいが、攻撃も強いので防いだ量も多い）</summary>
    public static int PostureMax(PieceInstance p) { return Mathf.Max(1, p.MaxHP); }

    private static bool IsBoss(PieceInstance p)
    {
        PieceType t = p.data.pieceType;
        return t == PieceType.Raitei || t == PieceType.Ryuujin || t == PieceType.Maou;
    }

    /// <summary>
    /// SEKIRO（成った・覚醒した RN）: 敵の通常の攻撃をブロックで防ぐと、その敵の体幹が防いだ量だけたまる（全部防いだ「弾き」はさらに+1）。
    /// 体幹が最大体力に届くと体幹崩しで忍殺（ふつうの敵は倒れ、ボスは最大体力の1/3を失って次の手番は動けない）
    /// </summary>
    public void OnPostureHit(PieceInstance rn, PieceInstance attacker, int blocked, bool parried)
    {
        if (rn == null || attacker == null || rn.data.pieceType != PieceType.RN || !RnMultiPlay(rn) || !rn.isAlive || !attacker.isAlive) return;
        if (attacker.team == rn.team || ResistsAbilities(attacker)) return;   // 僧兵は体幹も崩れない
        int gain = blocked + (parried ? BalanceTuning.RnPostureParryBonus : 0);
        attacker.posture += gain;
        int max = PostureMax(attacker);
        if (parried)
        {
            FloatingText.Spawn(rn.boardPosition, "弾き", Color.white, 3.4f);
            if (BattleEffects.Instance != null)
            {
                BattleEffects.Instance.PlayStream(rn.boardPosition, attacker.boardPosition, Color.white);
                if (rn.awakened) BattleEffects.Instance.PlayAwakenBurst("Ascension", rn.boardPosition, rn.data.awakenColor);
            }
        }
        FloatingText.Spawn(attacker.boardPosition, "体幹 " + Mathf.Min(attacker.posture, max) + "/" + max, PostureGold, 3f, 0.25f);
        CombatResolver.RefreshStats(attacker);
        RunRoster.Feat(rn);
        if (attacker.posture < max)
        {
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("RN", rn.team) + " が " + BattleLogUI.ColorName(attacker.DisplayName, attacker.team)
                    + " の攻撃を" + (parried ? "弾いた" : "受け止めた") + "（体幹 " + attacker.posture + "/" + max + "）");
            return;
        }
        Deathblow(rn, attacker);
    }

    /// <summary>体幹崩し → 忍殺</summary>
    private void Deathblow(PieceInstance rn, PieceInstance target)
    {
        bool boss = IsBoss(target);
        int damage = boss ? Mathf.CeilToInt(target.MaxHP / (float)Mathf.Max(1, BalanceTuning.RnDeathblowBossRatio)) : target.currentHP;
        target.posture = 0;
        FloatingText.Spawn(target.boardPosition, "忍殺", DeathblowRed, 4.6f);
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayDeathblow(rn.boardPosition, target.boardPosition);
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("RN", rn.team) + " が " + BattleLogUI.ColorName(target.DisplayName, target.team)
                + " の体幹を崩した。忍殺！" + (boss ? "（" + damage + "ダメージ、次の手番は動けない）" : ""));
        var source = GameSim.BeginSource(rn);
        bool killed = CombatResolver.ApplyDamage(target, damage, true);
        GameSim.EndSource(source);
        if (!killed && boss && target.isAlive)
        {
            target.stunned = true;
            target.stunHeld = true;
            CombatResolver.RefreshStats(target);
        }
    }

    private static readonly Color PostureGold = new Color(1f, 0.82f, 0.3f);
    private static readonly Color DeathblowRed = new Color(1f, 0.22f, 0.18f);

    // ============================================================
    // 通常の攻撃が当たって相手が生き残ったとき: 外跳の跳ね飛ばし、無糖（AC）のスタッガー
    // ============================================================
    public void OnHitAndSurvived(PieceInstance attacker, PieceInstance target)
    {
        if (attacker == null || target == null || !attacker.isAlive || !target.isAlive || IsSuppressed(attacker)) return;
        if (ResistsAbilities(target)) return;   // C3・僧兵は能力の影響を受けない
        if (attacker.data.pieceType == PieceType.Mutou && attacker.isPromoted)
        {
            if (target.staggered) return;
            target.staggered = true;
            CombatResolver.RefreshStats(target);
            FloatingText.Spawn(target.boardPosition, "スタッガー", EmberOrange, 3.2f, 0.25f);
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlayStream(attacker.boardPosition, target.boardPosition, EmberOrange);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("無糖", attacker.team) + " の機体が " + BattleLogUI.ColorName(target.DisplayName, target.team)
                    + " をスタッガーさせた（次に受けるダメージ+" + BalanceTuning.MutouStaggerDamage + "）");
            RunRoster.Feat(attacker);
        }
        else if (attacker.data.pieceType == PieceType.Negu && attacker.isPromoted)
        {
            Bounce(attacker, target);
        }
    }

    /// <summary>外跳ね: 相手を1マス向こうへ跳ね飛ばす。何かにぶつかったら（盤の端も）ダメージ（防御無視）</summary>
    private void Bounce(PieceInstance negu, PieceInstance target)
    {
        if (target.data.isImmovable || PieceTypes.IsShinkai(target.data.pieceType)) return;
        BoardManager bm = BoardManager.Instance;
        Vector2Int dest = Awakening.PushDestination(negu.boardPosition, target.boardPosition);
        bool blocked = !bm.IsInBounds(dest) || !bm.IsEmpty(dest);
        FloatingText.Spawn(target.boardPosition, blocked ? "跳ね返り" : "外跳ね", DailyPink, 3f, 0.2f);
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayDashStreak(negu.boardPosition, target.boardPosition, DailyPink, 0.25f);
        RunRoster.Feat(negu);
        if (!blocked)
        {
            CombatResolver.MovePieceTo(target, dest);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(negu.DisplayName, negu.team) + " が " + BattleLogUI.ColorName(target.DisplayName, target.team) + " を跳ね飛ばした");
            return;
        }
        int damage = CombatResolver.AbilityDamage(negu, BalanceTuning.NeguBounceWallDamage);
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(negu.DisplayName, negu.team) + " が跳ね飛ばした " + BattleLogUI.ColorName(target.DisplayName, target.team) + " がぶつかった（" + CombatResolver.AbilityHitPreview(target, damage) + "ダメージ）");
        var source = GameSim.BeginSource(negu);
        CombatResolver.AbilityHit(target, damage);
        GameSim.EndSource(source);
    }

    // ============================================================
    // 無糖: 死んで覚える・篝火
    // ============================================================

    /// <summary>自軍の無糖が倒された。死んで覚える（周のあいだ攻撃+1）を数え、覚醒していれば1局に1回は篝火から蘇る（蘇ったら true）</summary>
    public bool OnMutouDown(PieceInstance m)
    {
        GameManager gm = GameManager.Instance;
        bool learned = gm != null && gm.Roster != null && gm.Roster.OnDied(PieceType.Mutou);
        FloatingText.Spawn(m.boardPosition, "YOU DIED", DiedRed, 4f, 0.1f);
        SpeechBubble.Say(m, PieceLines.MutouDown);
        if (learned && BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("無糖", m.team) + " は死んで覚えた（次の局から攻撃+" + gm.Roster.DeathBonus(PieceType.Mutou) + "）");

        // 篝火: 覚醒していれば、1局に1回、自陣のいちばん手前から体力半分で蘇る
        if (!m.awakened || m.bonfireUsed) return false;
        m.bonfireUsed = true;
        if (learned) m.bonusATK += 1;   // 覚えたことは、その場でも活かす
        m.staggered = false;
        m.block = 0;
        m.currentHP = Mathf.Max(1, (m.MaxHP + 1) / 2);
        Vector2Int? home = HomeSquare(m.team);
        if (home.HasValue) CombatResolver.MovePieceTo(m, home.Value);
        CombatResolver.RefreshHP(m);
        CombatResolver.RefreshStats(m);
        FloatingText.Spawn(m.boardPosition, "篝火", EmberOrange, 3.8f, 0.4f);
        if (BattleEffects.Instance != null)
        {
            BattleEffects.Instance.PlayAwakenBurst("Bonfire", m.boardPosition, m.data.awakenColor, true);
            BattleEffects.Instance.PlayHealEffect(m.boardPosition);
        }
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("無糖", m.team) + " は満面の笑みで篝火から戻ってきた（体力" + m.currentHP + "）");
        return true;
    }

    // ============================================================
    // ニコの札（RN・寝癖・無糖）
    // ============================================================
    private bool DoGamerCard(PieceInstance niko, PieceType type)
    {
        switch (type)
        {
            case PieceType.RN:
            {
                // ブロック: ニコがブロックを得る（次の自分の手番の初めまで）
                int b = BalanceTuning.RnBlock + CardBonus(niko);
                if (niko.block >= b) return false;
                niko.block = b;
                FloatingText.Spawn(niko.boardPosition, "ブロック" + b, BlockSteel, 3f);
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayGuard(niko.boardPosition);
                return true;
            }
            case PieceType.Negu:
            {
                // デイリー報酬: 隣の味方1体の攻撃+1（1体に上限あり）
                var allies = AdjacentAllies(niko);
                allies.RemoveAll(a => SmallBuffOf(a) >= BalanceTuning.NikoBuffCap + CardBonus(niko));
                if (allies.Count == 0) return false;
                PieceInstance ally = allies[GameRng.Range(0, allies.Count)];
                ally.bonusATK += 1;
                smallBuffs[ally] = SmallBuffOf(ally) + 1;
                CombatResolver.RefreshStats(ally);
                FloatingText.Spawn(ally.boardPosition, "攻+1", Palette.ATK);
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayBuffEffect(ally.boardPosition, true);
                return true;
            }
            case PieceType.Mutou:
            {
                // スタッガー: 隣の敵1体（次に受けるダメージが増える）
                var targets = AdjacentEnemies(niko);
                targets.RemoveAll(t => t.staggered);
                if (targets.Count == 0) return false;
                PieceInstance target = targets[GameRng.Range(0, targets.Count)];
                target.staggered = true;
                CombatResolver.RefreshStats(target);
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayStream(niko.boardPosition, target.boardPosition, EmberOrange);
                FloatingText.Spawn(target.boardPosition, "スタッガー", EmberOrange, 3f);
                return true;
            }
            default:
                return false;
        }
    }
}
