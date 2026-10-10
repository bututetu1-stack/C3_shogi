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

    /// <summary>その局の前に選べる仲間・強化の枚数（BalanceTuning.TwoPickStages の局は2枚）</summary>
    public static int PicksForStage(int stage)
    {
        return Array.IndexOf(BalanceTuning.TwoPickStages, stage) >= 0 ? 2 : 1;
    }

    private BoardManager boardManager;
    private PieceSelectionUI pieceSelectionUI;
    private StageManager stageManager;
    private bool isGameOver;
    private int rerollsLeft;
    private int picksLeft;
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
    /// <summary>応急修理要員の残り（艦娘が沈むとき1回ずつ使う）</summary>
    public int RunDamageControl { get; private set; }
    // 艦隊がS勝利したら、次の局の選択肢に強化が1枚増える（その局の番号）
    private int bonusUpgradeStage;

    // 戦績
    public int TotalKills { get; private set; }
    public int TotalMoves { get; private set; }
    public int StagesCleared { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        // 音量は BGM と効果音に分けて SoundSettings で持つ（全体の音量は使わない）
        AudioListener.volume = 1f;
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
        RunDamageControl = 0;
        bonusUpgradeStage = 0;
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
                DeployRecruit(data, slot.Value);
        }
    }

    /// <summary>仲間の駒を自陣に置く（素の将棋駒は仲間としての上乗せ付き）</summary>
    private PieceInstance DeployRecruit(PieceData data, Vector2Int slot)
    {
        PieceInstance piece = boardManager.Spawn(data, Team.Player, slot);
        if (piece == null) return null;
        int hp = PiecePool.RecruitBonusHP(data);
        int atk = PiecePool.RecruitBonusATK(data);
        if (hp > 0 || atk > 0)
        {
            piece.AddMaxHP(hp);
            piece.bonusATK += atk;
            CombatResolver.RefreshStats(piece);
        }
        return piece;
    }

    // ================================================================
    // 仲間選択
    // ================================================================

    private void ShowPieceSelection(bool newStage)
    {
        SetPhase(GamePhase.PieceSelection);
        int stage = stageManager != null ? stageManager.currentStage : 1;
        if (newStage)
        {
            rerollsLeft = RerollsPerStage;
            picksLeft = PicksForStage(stage);
        }
        bool canDeploy = boardManager.FindPlayerDeploySlot().HasValue;
        List<DraftOption> options = DrawStageOptions(stage, canDeploy);

        if (options.Count == 0 || pieceSelectionUI == null)
        {
            StartBattle();
            return;
        }

        int pickCount = PicksForStage(stage);
        pieceSelectionUI.ShowSelection(options, OnOptionChosen, OnReroll, rerollsLeft, pickCount - picksLeft + 1, pickCount);
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
        picksLeft--;
        if (picksLeft > 0 && option != null) ShowPieceSelection(false);
        else StartBattle();
    }

    /// <summary>選んだ仲間を自陣に置く、または全軍強化を加える</summary>
    private void ApplyOption(DraftOption option)
    {
        if (option != null && option.piece != null)
        {
            Vector2Int? slot = boardManager.FindPlayerDeploySlot();
            if (slot.HasValue && DeployRecruit(option.piece, slot.Value) != null)
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
                case UpgradeKind.DamageControl: RunDamageControl += 1; break;
            }
        }
    }

    /// <summary>全軍強化を自軍の駒に反映する（ステージごとに駒を置き直すので毎回掛ける）</summary>
    private void ApplyRunBonuses()
    {
        foreach (var p in boardManager.GetTeamPieces(Team.Player))
            ApplyRunBonus(p);
    }

    private void ApplyRunBonus(PieceInstance p)
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

    /// <summary>
    /// 対局の途中で出てきた駒（艦娘・深海・黄泉の歩）に、その局のほかの駒と同じ強化を付ける。
    /// 能力による攻撃も、この攻撃の上乗せのぶん増える
    /// </summary>
    public void ApplySummonBonuses(PieceInstance p)
    {
        if (p == null) return;
        int stage = stageManager != null ? stageManager.currentStage : 1;
        if (PieceTypes.IsShinkai(p.data.pieceType))
        {
            // 深海の体力はランクで決まっているので、局による強化は攻撃だけ
            p.stageBonusATK = StageManager.EnemyStageAtk(stage);
            p.bonusATK += p.stageBonusATK;
            CombatResolver.RefreshStats(p);
        }
        else if (p.team == Team.Enemy)
        {
            StageManager.ScaleEnemy(p, stage);
        }
        else
        {
            StageManager.ScalePlayer(p, stage);
            ApplyRunBonus(p);
            // 艦娘は提督の練度として、敵と同じだけ攻撃も上がる
            if (PieceTypes.IsKanmusu(p.data.pieceType))
            {
                p.stageBonusATK = StageManager.EnemyStageAtk(stage);
                p.bonusATK += p.stageBonusATK;
                CombatResolver.RefreshStats(p);
            }
        }
    }

    /// <summary>その局の仲間選択の候補。艦隊がS勝利した次の局は、強化が1枚増える</summary>
    private List<DraftOption> DrawStageOptions(int stage, bool canDeploy)
    {
        List<DraftOption> options = PiecePool.DrawOptions(boardManager.allPieceData, 3, playerOwnedPieces, stage, canDeploy);
        if (stage == bonusUpgradeStage) PiecePool.AddExtraUpgrade(options, playerOwnedPieces);
        return options;
    }

    /// <summary>艦隊のS勝利: 次の局の選択肢に強化を1枚増やす</summary>
    public void GrantFleetBonus()
    {
        bonusUpgradeStage = (stageManager != null ? stageManager.currentStage : 1) + 1;
    }

    /// <summary>応急修理要員を1つ使う（残っていれば true）</summary>
    public bool UseDamageControl()
    {
        if (RunDamageControl <= 0) return false;
        RunDamageControl--;
        return true;
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

        // ボスが初めて出る局は登場のカットイン
        if (stageManager != null && !GameSim.Headless)
        {
            PieceType boss;
            PieceLines.CutIn cut = PieceLines.BossCutIn(stageManager.currentStage, out boss);
            if (cut != null)
            {
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayCutInSound();
                yield return CutInUI.PlayPiece(boardManager.GetPieceDataByType(boss), false, cut.title, cut.subtitle, cut.band, cut.accent, 1.6f);
            }
        }

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

    /// <summary>能力による移動などで敵陣に入ったとき（カットインは待たずに出す）</summary>
    public void CheckPromotion(PieceInstance piece)
    {
        if (ShouldPromote(piece, piece.boardPosition)) PromotePiece(piece);
    }

    /// <summary>能力による移動のあとの成り（カットインが終わるのを待つ版。なこの突撃など）</summary>
    public IEnumerator CheckPromotionRoutine(PieceInstance piece)
    {
        if (ShouldPromote(piece, piece.boardPosition)) yield return PromoteRoutine(piece);
    }

    /// <summary>成りの演出を待つ版（物鉄→提督は着任の演出が入る）</summary>
    public IEnumerator PromoteRoutine(PieceInstance piece)
    {
        if (piece.data.pieceType == PieceType.Monotetsu && AbilitySystem.Instance != null)
        {
            yield return AbilitySystem.Instance.TeitokuPromotionRoutine(piece);
            yield break;
        }
        // 部員の成りはカットインで見せる
        yield return PromotionCutIn(piece);
        PromotePiece(piece, false);
    }

    /// <summary>部員の成りのカットイン（ない駒・自動プレイ中は何もしない）</summary>
    private IEnumerator PromotionCutIn(PieceInstance piece)
    {
        if (GameSim.Headless || piece.team != Team.Player) yield break;
        PieceLines.CutIn cut = PieceLines.PromotionCutIn(piece.data.pieceType);
        if (cut == null) yield break;
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayCutInSound();
        yield return CutInUI.PlayPiece(piece.data, true, cut.title, cut.subtitle, cut.band, cut.accent, 1.4f);
    }

    /// <summary>
    /// 駒を成らせ、成りに伴う特殊処理（過労死・提督化）を行う。
    /// cutIn=true なら部員のカットインを待たずに出す（李白の裏返しなど、手番の途中で成るとき）
    /// </summary>
    public void PromotePiece(PieceInstance piece, bool cutIn = true)
    {
        if (piece == null || !piece.isAlive || piece.isPromoted || !piece.data.canPromote) return;

        // 大破進軍: 大破した艦娘が敵陣に入ると轟沈する
        if (AbilitySystem.SinksOnPromotion(piece))
        {
            FloatingText.Spawn(piece.boardPosition, "大破進軍", new Color(1f, 0.45f, 0.4f), 3.6f);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog("大破進軍……" + BattleLogUI.ColorName(piece.DisplayName, piece.team) + " は轟沈した");
            CombatResolver.KillPiece(piece);
            return;
        }
        if (cutIn && !GameSim.Headless && piece.data.pieceType != PieceType.Monotetsu)
            StartCoroutine(PromotionCutIn(piece));

        piece.Promote();
        GameSim.RecordPromotion(piece);
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
        return DrawStageOptions(stage, canDeploy);
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
