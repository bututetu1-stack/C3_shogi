using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>「もう一度挑む」で再読み込みしたときはタイトルを飛ばす</summary>
    public static bool SkipTitleOnce;

    public GamePhase currentPhase = GamePhase.Title;
    public Team currentTurn = Team.Player;

    public event Action<Team> OnTurnChanged;
    public event Action<GamePhase> OnPhaseChanged;
    public event Action<Team> OnGameOver;

    // ステージタイトル表示中に入力を止める時間（タイトルが消え始めたら操作できる）
    private const float StageIntroDuration = StageTitleUI.Duration - 0.3f;
    // 1ステージの手数の上限（超えたら判定）
    public const int MoveLimit = 150;
    // 1ステージで引き直せる回数
    private const int RerollsPerStage = 1;

    private BoardManager boardManager;
    private PieceSelectionUI pieceSelectionUI;
    private StageManager stageManager;
    private bool isGameOver;
    private int rerollsLeft;
    public bool IsTurnProcessing { get { return isTurnProcessing; } }
    private bool isTurnProcessing;

    /// <summary>現在のステージでの手数（1手目から）</summary>
    public int MoveCount { get; private set; }

    // プレイヤーが持っている駒データのリスト(ステージ間で引き継ぎ)
    private readonly List<PieceData> playerOwnedPieces = new List<PieceData>();
    public IList<PieceData> OwnedPieces { get { return playerOwnedPieces.AsReadOnly(); } }

    // 全軍強化（最後まで続く）
    public int RunBonusATK { get; private set; }
    public int RunBonusDEF { get; private set; }
    public int RunBonusHP { get; private set; }
    public int RunBonusC3HP { get; private set; }

    // 戦績
    public int TotalKills { get; private set; }
    public int TotalMoves { get; private set; }
    public int StagesCleared { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        AudioListener.volume = PlayerPrefs.GetFloat(TitleScreenUI.VolumeKey, 0.8f);
    }

    void Start()
    {
        boardManager = BoardManager.Instance;
        stageManager = StageManager.Instance;
        pieceSelectionUI = FindFirstObjectByType<PieceSelectionUI>();

        if (SkipTitleOnce)
        {
            SkipTitleOnce = false;
            StartNewGame();
        }
        else
        {
            SetPhase(GamePhase.Title);
            TitleScreenUI.Show(StartNewGame);
        }
    }

    private void StartNewGame()
    {
        ResetRun();
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayBGM("Battle");
        SetupStageBoard();
        ShowPieceSelection(true);
    }

    /// <summary>1周の状態（仲間・全軍強化・戦績・局）を最初に戻す</summary>
    private void ResetRun()
    {
        isGameOver = false;
        currentTurn = Team.Player;
        playerOwnedPieces.Clear();
        RunBonusATK = RunBonusDEF = RunBonusHP = RunBonusC3HP = 0;
        TotalKills = TotalMoves = StagesCleared = 0;
        if (stageManager != null) stageManager.currentStage = 1;
    }

    /// <summary>現在のステージの盤を作り、C3・歩・持ち越し駒を配置する</summary>
    private void SetupStageBoard()
    {
        int size = stageManager != null ? stageManager.GetBoardSize() : 5;
        boardManager.ClearAll();
        boardManager.InitBoard(size);

        if (AbilitySystem.Instance != null)
            AbilitySystem.Instance.ResetState();

        // C3を中央下に配置
        boardManager.SpawnPiece(boardManager.GetPieceDataByType(PieceType.C3), Team.Player, new Vector2Int(size / 2, 0));

        // 歩兵を配置（9x9なら3段目、それ以外は2段目）
        var pawn = boardManager.GetPieceDataByType(PieceType.Pawn);
        int pawnRow = (size >= 9) ? 2 : 1;
        for (int x = 0; x < size; x++)
            boardManager.SpawnPiece(pawn, Team.Player, new Vector2Int(x, pawnRow));

        // 持ち越し駒を外側から再配置
        foreach (var data in playerOwnedPieces)
        {
            var slot = boardManager.FindPlayerDeploySlot();
            if (slot.HasValue)
                boardManager.SpawnPiece(data, Team.Player, slot.Value);
        }
    }

    // ================================================================
    // 仲間選択
    // ================================================================

    private void ShowPieceSelection(bool newStage)
    {
        SetPhase(GamePhase.PieceSelection);
        if (newStage) rerollsLeft = RerollsPerStage;

        int stage = stageManager != null ? stageManager.currentStage : 1;
        bool canDeploy = boardManager.FindPlayerDeploySlot().HasValue;
        List<DraftOption> options = PiecePool.DrawOptions(boardManager.allPieceData, 3, playerOwnedPieces, stage, canDeploy);

        if (options.Count == 0 || pieceSelectionUI == null)
        {
            StartBattle();
            return;
        }

        pieceSelectionUI.ShowSelection(options, OnOptionChosen, OnReroll, rerollsLeft);
    }

    private void OnReroll()
    {
        if (rerollsLeft <= 0) return;
        rerollsLeft--;
        ShowPieceSelection(false);
    }

    private void OnOptionChosen(DraftOption option)
    {
        ApplyOption(option);
        StartBattle();
    }

    /// <summary>選んだ仲間を自陣に置く、または全軍強化を加える</summary>
    private void ApplyOption(DraftOption option)
    {
        if (option != null && option.piece != null)
        {
            Vector2Int? slot = boardManager.FindPlayerDeploySlot();
            if (slot.HasValue && boardManager.Spawn(option.piece, Team.Player, slot.Value) != null)
                playerOwnedPieces.Add(option.piece);
        }
        else if (option != null)
        {
            switch (option.upgrade)
            {
                case UpgradeKind.AllATK: RunBonusATK += 1; break;
                case UpgradeKind.AllDEF: RunBonusDEF += 1; break;
                case UpgradeKind.AllHP: RunBonusHP += 2; break;
                case UpgradeKind.C3HP: RunBonusC3HP += 4; break;
            }
        }
    }

    /// <summary>全軍強化を自軍の駒に反映する（ステージごとに駒を置き直すので毎回掛ける）</summary>
    private void ApplyRunBonuses()
    {
        foreach (var p in boardManager.GetTeamPieces(Team.Player))
        {
            if (p.data.pieceType == PieceType.C3)
            {
                p.AddMaxHP(RunBonusC3HP);
            }
            else
            {
                p.bonusATK += RunBonusATK;
                p.bonusDEF += RunBonusDEF;
                p.AddMaxHP(RunBonusHP);
            }
            CombatResolver.RefreshStats(p);
        }
    }

    // ================================================================
    // 対局の進行
    // ================================================================

    private void StartBattle()
    {
        StartCoroutine(BattleIntroSequence());
    }

    private IEnumerator BattleIntroSequence()
    {
        // タイトル表示中は入力を受け付けない
        isTurnProcessing = true;
        currentTurn = Team.Player;
        MoveCount = 1;

        if (stageManager != null)
        {
            stageManager.SetupEnemyForStage(stageManager.currentStage);

            // プレイヤー駒にもステージに応じた強化（ステージ2以降）
            if (stageManager.currentStage > 1)
                stageManager.ApplyPlayerScaling();
            ApplyRunBonuses();

            if (!GameSim.Headless)
                EnsureStageTitle().ShowTitle(stageManager.currentStage, stageManager.GetStageName(stageManager.currentStage));
        }

        // 対局開始SE
        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayBattleStartEffect();

        yield return new WaitForSeconds(StageIntroDuration);

        SetPhase(GamePhase.Battle);

        // 1手目からターン開始時能力（門人・ヲツ・なこ）を発動
        yield return ExecuteTurnStartThenNotify(Team.Player);
        isTurnProcessing = false;
    }

    private static StageTitleUI EnsureStageTitle()
    {
        if (StageTitleUI.Instance == null)
        {
            GameObject titleObj = new GameObject("StageTitleUI");
            titleObj.AddComponent<StageTitleUI>();
        }
        return StageTitleUI.Instance;
    }

    public void EndTurn()
    {
        if (isGameOver) return;
        if (isTurnProcessing) return;
        if (currentPhase != GamePhase.Battle) return;
        if (CheckGameOver()) return;
        StartCoroutine(EndTurnSequence());
    }

    private IEnumerator EndTurnSequence()
    {
        isTurnProcessing = true;

        // ターン終了時能力（中華回復消滅・深海攻撃・艦娘攻撃）
        if (AbilitySystem.Instance != null)
            yield return AbilitySystem.Instance.ExecuteTurnEndAbilities(currentTurn);

        if (isGameOver || CheckGameOver()) { isTurnProcessing = false; yield break; }

        currentTurn = (currentTurn == Team.Player) ? Team.Enemy : Team.Player;
        MoveCount++;

        // 手数の上限に達したら残った戦力で判定
        if (MoveCount > MoveLimit)
        {
            JudgeByStrength();
            isTurnProcessing = false;
            yield break;
        }

        // ターン開始時能力（門人自動移動・ヲツ中華生成）を実行してからOnTurnChanged
        yield return ExecuteTurnStartThenNotify(currentTurn);
        isTurnProcessing = false;
    }

    private IEnumerator ExecuteTurnStartThenNotify(Team team)
    {
        if (AbilitySystem.Instance != null)
            yield return AbilitySystem.Instance.ExecuteTurnStartAbilities(team);

        if (isGameOver || CheckGameOver()) yield break;

        if (OnTurnChanged != null && !GameSim.Headless) OnTurnChanged(currentTurn);
    }

    /// <summary>勝敗判定。決着（ステージクリア含む）したらtrue</summary>
    private bool CheckGameOver()
    {
        if (isGameOver) return true;
        if (currentPhase != GamePhase.Battle) return false;

        // C3撃破を最優先で判定
        if (boardManager.FindC3(Team.Enemy) == null)
        {
            HandleStageWon(null);
            return true;
        }
        if (boardManager.FindC3(Team.Player) == null)
        {
            HandleDefeat();
            return true;
        }

        // C3以外が全滅したら決着
        if (!HasFightingPieces(Team.Enemy))
        {
            HandleStageWon(null);
            return true;
        }
        if (!HasFightingPieces(Team.Player))
        {
            HandleDefeat();
            return true;
        }

        return false;
    }

    /// <summary>C3と一時的な召喚物（中華・ドパ）以外の駒が残っているか</summary>
    private bool HasFightingPieces(Team team)
    {
        foreach (var piece in boardManager.GetTeamPieces(team))
        {
            PieceType t = piece.data.pieceType;
            if (t == PieceType.C3 || t == PieceType.Chuka || t == PieceType.Dopa) continue;
            return true;
        }
        return false;
    }

    /// <summary>手数切れ: 残った駒の強さとC3の体力で勝敗を決める</summary>
    private void JudgeByStrength()
    {
        int player = Strength(Team.Player);
        int enemy = Strength(Team.Enemy);
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog("手数が" + MoveLimit + "手に達したため判定（" + player + " 対 " + enemy + "）");
        if (player >= enemy) HandleStageWon("判定勝ち");
        else HandleDefeat();
    }

    private int Strength(Team team)
    {
        int total = 0;
        foreach (var p in boardManager.GetTeamPieces(team))
        {
            if (p.data.pieceType == PieceType.C3) total += p.currentHP * 4;
            else total += p.ATK * 3 + p.DEF * 2 + Mathf.Min(p.currentHP, 20);
        }
        return total;
    }

    private void HandleStageWon(string reason)
    {
        TotalMoves += MoveCount;
        StagesCleared++;

        if (GameSim.Headless)
        {
            SimOutcome = reason != null ? SimBattleOutcome.JudgedWin : SimBattleOutcome.Won;
            if (stageManager == null || stageManager.IsLastStage()) isGameOver = true;
            SetPhase(GamePhase.StageClear);
            return;
        }

        if (stageManager != null && !stageManager.IsLastStage())
        {
            StartCoroutine(StageClearSequence(reason));
            return;
        }
        isGameOver = true;
        SetPhase(GamePhase.GameOver);
        if (OnGameOver != null) OnGameOver(Team.Player);
    }

    private void HandleDefeat()
    {
        TotalMoves += MoveCount;
        isGameOver = true;
        if (GameSim.Headless)
        {
            SimOutcome = MoveCount > MoveLimit ? SimBattleOutcome.JudgedLoss : SimBattleOutcome.Lost;
            SetPhase(GamePhase.GameOver);
            return;
        }
        SetPhase(GamePhase.GameOver);
        if (OnGameOver != null) OnGameOver(Team.Enemy);
    }

    /// <summary>ステージクリアの演出をしてから次のステージへ</summary>
    private IEnumerator StageClearSequence(string reason)
    {
        SetPhase(GamePhase.StageClear);
        isTurnProcessing = true;
        if (InputManager.Instance != null)
            InputManager.Instance.ClearSelection();

        int stage = stageManager.currentStage;
        EnsureStageTitle().ShowBanner(reason ?? "勝利",
            "第" + UIFactory.Kanji(stage) + "局「" + stageManager.GetStageName(stage) + "」突破", Palette.GoldLight);
        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayStageClearEffect();

        yield return new WaitForSeconds(StageTitleUI.Duration + 0.2f);

        isTurnProcessing = false;
        stageManager.AdvanceStage();
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.ClearLog();

        SetupStageBoard();
        ShowPieceSelection(true);
    }

    // ================================================================
    // 成り
    // ================================================================

    /// <summary>
    /// from から今の位置へ動いた駒が成るか。将棋と同じく、移動の前後どちらかが敵陣なら成る（成りは強制）
    /// </summary>
    public bool ShouldPromote(PieceInstance piece, Vector2Int from)
    {
        if (piece == null || !piece.isAlive || piece.isPromoted || !piece.data.canPromote) return false;
        int size = boardManager.CurrentBoardSize;
        return piece.CanPromoteAt(from.y, size) || piece.CanPromoteAt(piece.boardPosition.y, size);
    }

    /// <summary>能力による移動などで敵陣に入ったとき</summary>
    public void CheckPromotion(PieceInstance piece)
    {
        if (ShouldPromote(piece, piece.boardPosition)) PromotePiece(piece);
    }

    /// <summary>成りの演出を待つ版（物鉄→提督は着任の演出が入る）</summary>
    public IEnumerator PromoteRoutine(PieceInstance piece)
    {
        if (piece.data.pieceType == PieceType.Monotetsu && AbilitySystem.Instance != null)
        {
            yield return AbilitySystem.Instance.TeitokuPromotionRoutine(piece);
            yield break;
        }
        PromotePiece(piece);
    }

    /// <summary>駒を成らせ、成りに伴う特殊処理（過労死・提督化）を行う</summary>
    public void PromotePiece(PieceInstance piece)
    {
        if (piece == null || !piece.isAlive || piece.isPromoted || !piece.data.canPromote) return;

        piece.Promote();
        CombatResolver.PlayFlip(piece);
        FloatingText.Spawn(piece.boardPosition, "成", Palette.GoldLight, 4f);
        SpeechBubble.Say(piece, PieceLines.OnPromote(piece.data.pieceType));

        // 過労死チェック（SN・小錦）
        if (piece.data.diesOnPromotion)
        {
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(piece.DisplayName, piece.team) + " は力尽きた...");
            CombatResolver.KillPiece(piece);
            return;
        }

        // 物鉄→提督の特殊処理（演出つき。通常は PromoteRoutine から呼ばれる）
        if (piece.data.pieceType == PieceType.Monotetsu && AbilitySystem.Instance != null)
        {
            IEnumerator arrival = AbilitySystem.Instance.TeitokuArrivalRoutine(piece);
            if (GameSim.Headless) GameSim.RunSync(arrival);
            else AbilitySystem.Instance.StartCoroutine(arrival);
        }
    }

    // ================================================================
    // その他
    // ================================================================

    public void RegisterKill()
    {
        TotalKills++;
    }

    public void Resign()
    {
        if (isGameOver) return;
        StopAllCoroutines();
        isTurnProcessing = false;
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("あなた", Team.Player) + " は投了した");
        HandleDefeat();
    }

    public void SetPhase(GamePhase phase)
    {
        currentPhase = phase;
        if (OnPhaseChanged != null && !GameSim.Headless) OnPhaseChanged(phase);
    }

    // ================================================================
    // 自動プレイ（バランステスト）用。GameSim.Headless の間に BalanceSimulator から呼ぶ
    // ================================================================

    /// <summary>直前の対局の結果（Headless のときだけ記録する）</summary>
    public SimBattleOutcome SimOutcome { get; private set; }

    /// <summary>新しい周を始め、第一局の盤（C3と歩）を用意する</summary>
    public void SimBeginRun()
    {
        if (boardManager == null) boardManager = BoardManager.Instance;
        if (stageManager == null) stageManager = StageManager.Instance;
        StopAllCoroutines();
        isTurnProcessing = false;
        ResetRun();
        SetupStageBoard();
    }

    /// <summary>今の局の仲間選択の候補（本番と同じ抽選）</summary>
    public List<DraftOption> SimDrawOptions()
    {
        int stage = stageManager != null ? stageManager.currentStage : 1;
        bool canDeploy = boardManager.FindPlayerDeploySlot().HasValue;
        return PiecePool.DrawOptions(boardManager.allPieceData, 3, playerOwnedPieces, stage, canDeploy);
    }

    public void SimApplyOption(DraftOption option) { ApplyOption(option); }

    /// <summary>敵の配置・強化をして1手目のターン開始時能力まで進める</summary>
    public IEnumerator SimStartBattle()
    {
        SimOutcome = SimBattleOutcome.None;
        return BattleIntroSequence();
    }

    /// <summary>手番を終える（ターン終了時能力 → 勝敗判定 → 手番交代 → ターン開始時能力）</summary>
    public IEnumerator SimEndTurn()
    {
        if (isGameOver || CheckGameOver()) return null;
        return EndTurnSequence();
    }

    /// <summary>次の局へ進み、盤を用意する</summary>
    public void SimNextStage()
    {
        stageManager.AdvanceStage();
        SetupStageBoard();
    }
}

/// <summary>自動プレイで記録する1局の結果</summary>
public enum SimBattleOutcome
{
    None,
    Won,
    JudgedWin,
    Lost,
    JudgedLoss
}
