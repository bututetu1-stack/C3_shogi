using UnityEngine;
using System.Collections;

/// <summary>
/// 駒の移動・攻撃・ダメージ・撃破の処理を一元化する。
/// プレイヤー操作・AI・自動移動（門人）・各種能力は全てここを通す。
/// </summary>
public static class CombatResolver
{
    /// <summary>通常攻撃のダメージ = 攻撃（＋ユウの助走）− 防御。0未満にはならない。潜水艦には0</summary>
    public static int CalcDamage(PieceInstance attacker, PieceInstance target)
    {
        // 潜水艦は通常の攻撃を受けない（能力の攻撃は受ける）
        if (target.data.pieceType == PieceType.KanmusuSS) return 0;
        return Mathf.Max(0, attacker.ATK + RunUpBonus(attacker, target) - target.DEF);
    }

    /// <summary>ユウの助走: 敵まで走ったマスのうち、2マス目から1マスごとに攻撃が上がる（攻撃の前の位置で測る）</summary>
    public static int RunUpBonus(PieceInstance attacker, PieceInstance target)
    {
        if (attacker.data.pieceType != PieceType.Yuu) return 0;
        Vector2Int d = target.boardPosition - attacker.boardPosition;
        int distance = Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));
        return Mathf.Max(0, distance - 1) * BalanceTuning.YuuRunUpPerSquare;
    }

    /// <summary>
    /// 能力による攻撃（なこの突撃・深海・艦娘・魔王の雷撃・髑髏の爆発）のダメージ。
    /// 能力ごとの基本の値に、出どころの駒の攻撃の上乗せ（局による強化・僕・軍将・全軍強化、けいに下げられた分）を足す。最低1。
    /// withStageBonus=false なら局による強化の分は足さない（魔王の雷撃）
    /// </summary>
    public static int AbilityDamage(PieceInstance source, int baseDamage, bool withStageBonus = true)
    {
        if (source == null) return baseDamage;
        int bonus = source.bonusATK - (withStageBonus ? 0 : source.stageBonusATK);
        return Mathf.Max(1, baseDamage + bonus);
    }

    // ================================================================
    // 1手の実行（攻撃 → 撃破なら前進 → SN消耗 → 成り → 李白）
    // ================================================================
    public static IEnumerator ExecuteMove(PieceInstance piece, MoveValidator.MoveResult move)
    {
        if (piece == null || !piece.isAlive) yield break;

        BoardManager bm = BoardManager.Instance;
        Vector2Int from = piece.boardPosition;
        Vector2Int to = move.position;

        if (move.isAttack)
        {
            PieceInstance target = bm.GetPieceAt(to);
            if (target != null && target.team != piece.team)
            {
                // 踏み込んでから当たる
                PieceController attackerPC = bm.GetPieceController(from);
                if (attackerPC != null) attackerPC.Lunge(to);
                yield return new WaitForSeconds(0.09f);

                int runUp = RunUpBonus(piece, target);
                bool killed = Attack(piece, target);
                ShowLastMove(from, to);
                // 覚醒したユウ「ゾーン」: 助走で倒したら、もう一度動ける（C3 を倒したら決着なので出さない）
                if (killed && piece.awakened && runUp > 0 && target.data.pieceType != PieceType.C3 && GameManager.Instance != null)
                    GameManager.Instance.GrantBonusMove(piece);

                // 撃破できなければ攻撃側はその場に留まる
                if (!killed)
                {
                    yield return new WaitForSeconds(0.3f);
                    yield break;
                }
                yield return new WaitForSeconds(0.12f);
            }
        }

        // 髑髏の爆発などで攻撃側が倒れた場合はここで終了
        if (!piece.isAlive || !bm.IsEmpty(to)) yield break;

        // SN は残像を残して駆け抜ける
        if (piece.data.losesHPOnMove)
        {
            PieceController driver = bm.GetPieceController(from);
            if (driver != null) driver.SetTrail(new Color(0.75f, 0.75f, 0.8f, 0.45f), 0.3f);
        }
        MovePieceTo(piece, to);
        ShowLastMove(from, to);
        if (BattleEffects.Instance != null)
        {
            BattleEffects.Instance.PlayMoveEffect();
            // SN はドライブで駆け抜ける
            if (piece.data.losesHPOnMove) BattleEffects.Instance.PlayExhaust(from, to);
        }
        yield return new WaitForSeconds(PieceController.MoveDuration);

        // SN: 移動でHP-1
        if (piece.data.losesHPOnMove)
        {
            piece.currentHP--;
            RefreshHP(piece);
            FloatingText.Spawn(piece.boardPosition, "-1", PiercingColor, 2.8f);
            if (piece.currentHP <= 0)
            {
                Log(Name(piece) + " は力尽きた...");
                SpeechBubble.Say(piece, PieceLines.SnExhausted);
                KillPiece(piece);
                yield break;
            }
        }

        // 成り（将棋と同じく、敵陣に入る・敵陣から出る手で成る。成りは強制）
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.ShouldPromote(piece, from))
            yield return gm.PromoteRoutine(piece);
        else if (AbilitySystem.Instance != null)
            AbilitySystem.Instance.CheckKai2(piece, from);   // 改のまま敵陣を出た艦娘は改二
        if (!piece.isAlive) yield break;

        // 李白の裏返し能力（移動後に発動）
        if (piece.data.pieceType == PieceType.Rihaku && AbilitySystem.Instance != null)
            AbilitySystem.Instance.ExecuteRihakuAbility(piece);
    }

    /// <summary>通常攻撃。撃破したらtrue</summary>
    public static bool Attack(PieceInstance attacker, PieceInstance target)
    {
        // 覚醒した僕の身代わり（倒されるはずの味方の代わりに受ける）
        if (CalcDamage(attacker, target) >= target.currentHP)
        {
            PieceInstance sub = TakeSubstitute(target);
            if (sub != null)
            {
                Attack(attacker, sub);
                return false;
            }
        }

        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlaySlash(target.boardPosition, attacker.boardPosition);

        int runUp = RunUpBonus(attacker, target);
        int damage = CalcDamage(attacker, target);
        target.currentHP -= damage;
        RefreshHP(target);
        // 小錦がいじめられると周りが同情して奮起する
        if (target.data.pieceType == PieceType.Konishiki && AbilitySystem.Instance != null)
        {
            AbilitySystem.Instance.Sympathize(target);
            RunRoster.Feat(target);
        }
        if (attacker.data.pieceType == PieceType.Yuu)
        {
            if (runUp > 0)
            {
                RunRoster.Feat(attacker);
                FloatingText.Spawn(attacker.boardPosition, "助走+" + runUp, YuuOrange, 2.8f);
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayDashStreak(attacker.boardPosition, target.boardPosition, YuuOrange, 0.25f);
            }
            SpeechBubble.SayMaybe(attacker, PieceLines.YuuAttack, 0.5f);
        }

        bool saved = AbilitySystem.Instance != null && AbilitySystem.Instance.OnShipHit(target);
        if (target.currentHP <= 0 && !saved)
        {
            FloatingText.Spawn(target.boardPosition, "撃破", Palette.GoldLight, 3.6f);
            Log(Name(attacker) + " が " + Name(target) + " を撃破！");
            // きぷは弱い武器で倒して煽る
            if (attacker.data.pieceType == PieceType.Kipu && !attacker.isPromoted) SpeechBubble.Say(attacker, PieceLines.KipuTaunt);
            GameSim.SetAttacker(attacker);
            KillPiece(target);
            GameSim.SetAttacker(null);
            return true;
        }

        CheckBossAwakening(target);
        PlayHit(target);
        FloatingText.Spawn(target.boardPosition, damage > 0 ? "-" + damage : "0", damage > 0 ? DamageColor : Palette.TextSub);
        Log(Name(attacker) + " → " + Name(target) + " " + damage + "ダメージ");
        if (target.data.isTauntPiece)
        {
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlayGuard(target.boardPosition);
            SpeechBubble.SayMaybe(target, PieceLines.KonishikiBullied, 0.5f);
        }
        return false;
    }

    private static readonly Color DamageColor = new Color(1f, 0.5f, 0.4f);
    private static readonly Color YuuOrange = new Color(1f, 0.6f, 0.25f);
    private static readonly Color PiercingColor = new Color(0.85f, 0.6f, 1f);
    public static readonly Color HealColor = new Color(0.5f, 0.95f, 0.55f);

    /// <summary>能力によるダメージ。piercing=trueでDEFを無視。撃破したらtrue</summary>
    public static bool ApplyDamage(PieceInstance target, int amount, bool piercing)
    {
        if (target == null || !target.isAlive) return false;

        int damage = piercing ? amount : Mathf.Max(0, amount - target.DEF);
        if (damage >= target.currentHP)
        {
            PieceInstance sub = TakeSubstitute(target);
            if (sub != null)
            {
                ApplyDamage(sub, amount, piercing);
                return false;
            }
        }
        target.currentHP -= damage;
        RefreshHP(target);
        FloatingText.Spawn(target.boardPosition, damage > 0 ? "-" + damage : "0", damage > 0 ? (piercing ? PiercingColor : DamageColor) : Palette.TextSub);

        bool saved = AbilitySystem.Instance != null && AbilitySystem.Instance.OnShipHit(target);
        if (target.currentHP <= 0 && !saved)
        {
            KillPiece(target);
            return true;
        }

        CheckBossAwakening(target);
        PlayHit(target);
        return false;
    }

    /// <summary>敵ボス（雷帝・龍神・魔王）は、体力が半分を切った最初のときに覚醒する（第二形態）</summary>
    private static void CheckBossAwakening(PieceInstance p)
    {
        if (p == null || !p.isAlive || p.team != Team.Enemy || p.awakened || !p.data.canAwaken) return;
        if (p.currentHP * 2 > p.MaxHP) return;
        if (GameManager.Instance != null) GameManager.Instance.Awaken(p);
        else Awakening.Apply(p);
    }

    /// <summary>
    /// 覚醒した僕の身代わり: 倒されそうな味方（C3・僕を除く）の隣に、まだ身代わりを使っていない覚醒した僕がいれば、
    /// その僕を返して使ったことにする（1局に1回）
    /// </summary>
    private static PieceInstance TakeSubstitute(PieceInstance target)
    {
        if (target == null || target.data.pieceType == PieceType.C3 || target.data.pieceType == PieceType.Boku) return null;
        BoardManager bm = BoardManager.Instance;
        if (bm == null) return null;
        foreach (var p in bm.GetTeamPieces(target.team))
        {
            if (p.data.pieceType != PieceType.Boku || !p.awakened || p.substituteUsed || !p.isAlive) continue;
            if (Mathf.Max(Mathf.Abs(p.boardPosition.x - target.boardPosition.x), Mathf.Abs(p.boardPosition.y - target.boardPosition.y)) != 1) continue;
            p.substituteUsed = true;
            FloatingText.Spawn(p.boardPosition, "身代わり", Palette.GoldLight, 3.4f);
            Log(Name(p) + " が " + Name(target) + " の身代わりになった");
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlayMentorBeam(target.boardPosition, p.boardPosition);
            return p;
        }
        return null;
    }

    /// <summary>回復（最大HPまで）。回復量を返す</summary>
    public static int Heal(PieceInstance target, int amount)
    {
        if (target == null || !target.isAlive) return 0;
        int healed = Mathf.Min(amount, target.MaxHP - target.currentHP);
        if (healed <= 0) return 0;
        target.currentHP += healed;
        RefreshHP(target);
        FloatingText.Spawn(target.boardPosition, "+" + healed, HealColor);
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayHealEffect(target.boardPosition);
        return healed;
    }

    /// <summary>成り・成り解除の見た目を更新する（裏返す演出つき）</summary>
    public static void PlayFlip(PieceInstance piece)
    {
        PieceController pc = BoardManager.Instance.GetPieceController(piece.boardPosition);
        if (pc != null) pc.PlayPromote();
        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayPromoteEffect(piece.boardPosition);
    }

    private static void ShowLastMove(Vector2Int from, Vector2Int to)
    {
        if (HighlightManager.Instance != null)
            HighlightManager.Instance.ShowLastMove(from, to);
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
        // 先代部長（SN）の引退
        if (target.data.pieceType == PieceType.SN && target.team == Team.Player && GameManager.Instance != null)
            GameManager.Instance.PlaySnRetire(target);
        if (target.data.pieceType == PieceType.Monotetsu && !target.isPromoted)
            SpeechBubble.Say(target, PieceLines.MonotetsuDown);

        // 海の駒（艦娘・深海）は撃破ではなく轟沈の演出で沈む
        bool atSea = PieceTypes.IsKanmusu(target.data.pieceType) || PieceTypes.IsShinkai(target.data.pieceType);
        if (BattleEffects.Instance != null)
        {
            if (atSea) BattleEffects.Instance.PlaySinkEffect(pos, target.team == Team.Player);
            else BattleEffects.Instance.PlayDefeatEffect(pos);
        }
        if (target.team == Team.Enemy && GameManager.Instance != null)
        {
            GameManager.Instance.RegisterKill();
            GameManager.Instance.Roster.OnKill(GameSim.CurrentKiller, target);
        }
        GameSim.RecordKill(target);

        if (atSea)
        {
            PieceController pc = bm.DetachPieceController(pos);
            bm.RemovePiece(pos);
            if (pc != null) pc.PlayExit(PieceController.ExitStyle.Sink);
        }
        else
        {
            bm.RemovePieceController(pos);
            bm.RemovePiece(pos);
        }

        if (checkLinkedDeaths && AbilitySystem.Instance != null)
            AbilitySystem.Instance.CheckLinkedDeaths(groupId, target);
    }

    /// <summary>撃破ではない形で盤から去らせる（艦隊の帰投・沈没など）。死亡時能力や撃破数には数えない</summary>
    public static void RemoveWithExit(PieceInstance piece, PieceController.ExitStyle style)
    {
        if (piece == null || !piece.isAlive) return;
        BoardManager bm = BoardManager.Instance;
        Vector2Int pos = piece.boardPosition;
        PieceController pc = bm.DetachPieceController(pos);
        bm.RemovePiece(pos, false);
        if (pc != null) pc.PlayExit(style);
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
