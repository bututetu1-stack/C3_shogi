using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 対人戦（PvpMatch）の流れ: 2人の編成で盤を作り、交代で指し、決着をつける。
/// 練度・覚醒・局の上乗せ・中断は使わない。いまは1台で2人が交代で指す（オンラインは次の段）。
/// </summary>
public partial class GameManager
{
    /// <summary>対人戦の決着が引き分けだった（決着画面で使う）</summary>
    public bool PvpDraw { get; private set; }

    /// <summary>この陣営を人が動かせるか（対人戦は両方。1人用は自軍だけ）</summary>
    public bool IsHumanTeam(Team team)
    {
        if (PvpMatch.Active) return !PvpMatch.Online || team == PvpMatch.LocalTeam;
        return team == Team.Player;
    }

    // 対人戦で並べた駒（陣営ごと、編成の順。オンラインの並べ方の受け渡しに使う）
    private readonly List<PieceInstance> pvpFirstPieces = new List<PieceInstance>();
    private readonly List<PieceInstance> pvpSecondPieces = new List<PieceInstance>();
    private List<PieceInstance> PvpPiecesOf(Team team) { return team == Team.Player ? pvpFirstPieces : pvpSecondPieces; }

    /// <summary>オンラインで相手が投了した</summary>
    public void RemoteResign()
    {
        if (isGameOver || !PvpMatch.Active) return;
        StopAllCoroutines();
        isTurnProcessing = false;
        Team loser = PvpMatch.LocalTeam == Team.Player ? Team.Enemy : Team.Player;
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("相手", loser) + " は投了した");
        EndPvp(PvpMatch.LocalTeam, "投了");
    }

    /// <summary>タイトルの「対人戦」: ルールを選ぶ画面から始める</summary>
    private void BeginPvp()
    {
        RunSave.Delete();
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayBGM("Battle");
        PvpSetupUI.Show(StartPvpBattle, ReturnToTitle);
    }

    private void ReturnToTitle()
    {
        PvpMatch.End();
        ViewFlip.Set(false);
        if (PvpOnline.Instance != null) PvpOnline.Instance.Leave();
        SkipTitleOnce = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    /// <summary>2人の編成がそろったら、盤を作って対局を始める</summary>
    public void StartPvpBattle()
    {
        // オンラインの後手は、盤を180度回して見る（自分の駒が手前）
        ViewFlip.Set(PvpMatch.Online && PvpMatch.LocalTeam == Team.Enemy);
        isGameOver = false;
        PvpDraw = false;
        currentTurn = Team.Player;
        playerOwnedPieces.Clear();
        Roster.Clear();
        RunBonusATK = RunBonusDEF = RunBonusHP = RunBonusC3HP = 0;
        RunDamageControl = 0;
        RunNoPawns = false;
        SecretFund = 0;
        EnemySecretFund = 0;
        FleetSWins = 0;
        RingedClasses.Clear();
        bonusUpgradeStage = 0;
        pendingExtraPicks = pendingExtraRerolls = pendingEnemyHp = pendingEnemyAtk = 0;
        TotalKills = TotalMoves = StagesCleared = 0;
        if (stageManager != null) stageManager.currentStage = 1;

        int size = PvpMatch.BoardSize;
        boardManager.ClearAll();
        boardManager.InitBoard(size);
        if (AbilitySystem.Instance != null) AbilitySystem.Instance.ResetState();
        if (BattleLogUI.Instance != null) BattleLogUI.Instance.ClearLog();

        // C3 は手前の段の真ん中（後手は奥の段）。後手は1手遅いぶん体力を少し足す
        PieceData c3 = boardManager.GetPieceDataByType(PieceType.C3);
        boardManager.SpawnPiece(c3, Team.Player, new Vector2Int(size / 2, 0));
        PieceInstance c3Second = boardManager.Spawn(c3, Team.Enemy, new Vector2Int(size / 2, size - 1));
        if (c3Second != null && PvpMatch.SecondC3Bonus > 0) c3Second.AddMaxHP(PvpMatch.SecondC3Bonus);

        // 巨岩は盤の中心で点対称に。下半分の岩は後手のもの（先手が壊せる）、上半分は先手のもの
        PieceData rock = boardManager.GetPieceDataByType(PieceType.Kyogan);
        if (rock != null)
        {
            var spots = PvpMatch.RockSpots();
            for (int i = 0; i < spots.Count; i++)
            {
                Vector2Int s = spots[i];
                Team owner = s.y < size / 2 ? Team.Enemy : s.y > size / 2 ? Team.Player : (i % 2 == 0 ? Team.Enemy : Team.Player);
                boardManager.SpawnPiece(rock, owner, s);
            }
        }

        // 編成した駒を自陣に決まった順で並べる（このあと並べ方で動かせる）
        pvpFirstPieces.Clear();
        pvpSecondPieces.Clear();
        foreach (var side in new[] { PvpMatch.First, PvpMatch.Second })
        {
            var order = PvpMatch.PlacementOrder(side.team);
            int next = 0;
            foreach (var data in side.pieces)
            {
                while (next < order.Count && !boardManager.IsEmpty(order[next])) next++;
                if (next >= order.Count) break;
                PieceInstance p = boardManager.Spawn(data, side.team, order[next]);
                if (p == null) continue;
                int hp = PiecePool.RecruitBonusHP(data);
                int atk = PiecePool.RecruitBonusATK(data);
                if (hp > 0) p.AddMaxHP(hp);
                p.bonusATK += atk;
                PvpPiecesOf(side.team).Add(p);
                next++;
            }
            if (side.team == Team.Player) playerOwnedPieces.AddRange(side.pieces);
        }

        // 全軍強化
        foreach (var p in boardManager.GetTeamPieces(Team.Player)) ApplyPvpUpgrades(p);
        foreach (var p in boardManager.GetTeamPieces(Team.Enemy)) ApplyPvpUpgrades(p);

        if (GameSim.Headless) { StartCoroutine(PvpIntroSequence()); return; }
        if (PvpMatch.Online)
        {
            // オンライン: 自分の駒だけ並べて送り、相手の並べ方が届いたら入れて対局へ
            Team me = PvpMatch.LocalTeam;
            Team foe = me == Team.Player ? Team.Enemy : Team.Player;
            PvpPlacement.Begin(me, () =>
            {
                PvpOnline.Instance.SendPlacement(PvpPiecesOf(me));
                StartCoroutine(WaitPeerPlacement(foe));
            });
            return;
        }
        // 並べ方: 先手 → 後手（並べているあいだ相手の駒は隠す）→ 対局
        PvpSetupUI.ShowHandoff(Team.Player, "の並べ方", () => PvpPlacement.Begin(Team.Player, () =>
            PvpSetupUI.ShowHandoff(Team.Enemy, "の並べ方", () => PvpPlacement.Begin(Team.Enemy, () =>
            {
                PvpPlacement.RevealAll();
                StartCoroutine(PvpIntroSequence());
            }))));
    }

    /// <summary>その陣営の全軍強化を駒に付ける（途中で出てきた駒にも使う）</summary>
    private void ApplyPvpUpgrades(PieceInstance p)
    {
        if (p == null || AbilitySystem.IsObstacle(p.data.pieceType)) return;
        PvpMatch.Side side = PvpMatch.SideOf(p.team);
        foreach (var u in side.upgrades)
        {
            if (p.data.pieceType == PieceType.C3)
            {
                if (u == UpgradeKind.C3HP) p.AddMaxHP(4);
                continue;
            }
            switch (u)
            {
                case UpgradeKind.AllATK: p.bonusATK += 1; break;
                case UpgradeKind.AllDEF: p.bonusDEF += 1; break;
                case UpgradeKind.AllHP: p.AddMaxHP(2); break;
            }
        }
        CombatResolver.RefreshStats(p);
    }

    private IEnumerator WaitPeerPlacement(Team foe)
    {
        if (PvpOnline.Instance.PeerPlace == null)
            PvpSetupUI.ShowWaiting("相手の並べ方を待っています");
        while (PvpOnline.Instance.PeerPlace == null) yield return null;
        PvpSetupUI.HideWaiting();
        PvpOnline.ApplyPlacement(PvpOnline.Instance.PeerPlace, PvpPiecesOf(foe));
        PvpPlacement.RevealAll();
        yield return PvpIntroSequence();
    }

    private IEnumerator PvpIntroSequence()
    {
        isTurnProcessing = true;
        currentTurn = Team.Player;
        MoveCount = 1;
        if (!GameSim.Headless)
            EnsureStageTitle().ShowBanner("対人戦", PvpMatch.CurrentRule == PvpMatch.Rule.Cost ? "コスト制" : "ランダム配布", Palette.Text);
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayBattleStartEffect();
        yield return new WaitForSeconds(StageIntroDuration);
        SetPhase(GamePhase.Battle);
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(PvpMatch.Online
                ? "対人戦の始まり。" + (PvpMatch.LocalTeam == Team.Player ? "あなたが先手（先に指す）" : "あなたは後手（相手が先に指す）")
                : "対人戦の始まり。" + BattleLogUI.ColorName("先手", Team.Player) + "から指す");
        yield return ExecuteTurnStartThenNotify(Team.Player);
        isTurnProcessing = false;
    }

    /// <summary>対人戦の決着（winner が null なら引き分け）</summary>
    private void EndPvp(Team? winner, string reason)
    {
        isGameOver = true;
        PvpDraw = !winner.HasValue;
        SetPhase(GamePhase.GameOver);
        if (InputManager.Instance != null) InputManager.Instance.ClearSelection();
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(winner.HasValue
                ? BattleLogUI.ColorName(PvpMatch.SideName(winner.Value), winner.Value) + " の勝ち" + (reason != null ? "（" + reason + "）" : "")
                : "引き分け" + (reason != null ? "（" + reason + "）" : ""));
        if (OnGameOver != null) OnGameOver(winner ?? Team.Player);
    }

    /// <summary>手数切れの判定（対人戦）: 強さが同じなら引き分け</summary>
    private void JudgePvp(int first, int second)
    {
        if (first == second) EndPvp(null, "判定");
        else EndPvp(first > second ? Team.Player : Team.Enemy, "判定");
    }
}
