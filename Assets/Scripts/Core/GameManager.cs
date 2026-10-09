using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GamePhase currentPhase = GamePhase.Battle;
    public Team currentTurn = Team.Player;

    public event Action<Team> OnTurnChanged;
    public event Action<GamePhase> OnPhaseChanged;
    public event Action<Team> OnGameOver;

    // ステージタイトル表示中に入力を止める時間（タイトルが消え始めたら操作できる）
    private const float StageIntroDuration = StageTitleUI.Duration - 0.3f;

    private BoardManager boardManager;
    private PieceSelectionUI pieceSelectionUI;
    private StageManager stageManager;
    private bool isGameOver;
    public bool IsTurnProcessing { get { return isTurnProcessing; } }
    private bool isTurnProcessing;

    /// <summary>現在のステージでの手数（1手目から）</summary>
    public int MoveCount { get; private set; }

    // プレイヤーが持っている駒データのリスト(ステージ間で引き継ぎ)
    private List<PieceData> playerOwnedPieces = new List<PieceData>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        boardManager = BoardManager.Instance;
        stageManager = StageManager.Instance;
        pieceSelectionUI = FindFirstObjectByType<PieceSelectionUI>();

        StartNewGame();
    }

    private void StartNewGame()
    {
        isGameOver = false;
        currentTurn = Team.Player;
        playerOwnedPieces.Clear();

        SetupStageBoard();
        ShowPieceSelection();
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

    private void ShowPieceSelection()
    {
        SetPhase(GamePhase.PieceSelection);

        List<PieceData> onBoard = boardManager.GetPiecesOnBoard();
        List<PieceData> choices = PiecePool.DrawPieces(boardManager.allPieceData, 3, onBoard);

        if (choices.Count == 0 || pieceSelectionUI == null || !boardManager.FindPlayerDeploySlot().HasValue)
        {
            StartBattle();
            return;
        }

        pieceSelectionUI.ShowSelection(choices, OnPieceChosen);
    }

    private void OnPieceChosen(PieceData chosen)
    {
        if (chosen != null)
        {
            Vector2Int? slot = boardManager.FindPlayerDeploySlot();
            if (slot.HasValue && boardManager.SpawnPiece(chosen, Team.Player, slot.Value) != null)
                playerOwnedPieces.Add(chosen);
        }

        StartBattle();
    }

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

            if (StageTitleUI.Instance == null)
            {
                GameObject titleObj = new GameObject("StageTitleUI");
                titleObj.AddComponent<StageTitleUI>();
            }
            StageTitleUI.Instance.ShowTitle(stageManager.currentStage, stageManager.GetStageName(stageManager.currentStage));
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

        // ターン開始時能力（門人自動移動・ヲツ中華生成）を実行してからOnTurnChanged
        yield return ExecuteTurnStartThenNotify(currentTurn);
        isTurnProcessing = false;
    }

    private IEnumerator ExecuteTurnStartThenNotify(Team team)
    {
        if (AbilitySystem.Instance != null)
            yield return AbilitySystem.Instance.ExecuteTurnStartAbilities(team);

        if (isGameOver || CheckGameOver()) yield break;

        if (OnTurnChanged != null) OnTurnChanged(currentTurn);
    }

    /// <summary>勝敗判定。決着（ステージクリア含む）したらtrue</summary>
    private bool CheckGameOver()
    {
        if (isGameOver) return true;
        if (currentPhase != GamePhase.Battle) return false;

        // C3撃破を最優先で判定
        if (boardManager.FindC3(Team.Enemy) == null)
        {
            HandleStageWon();
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
            HandleStageWon();
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

    private void HandleStageWon()
    {
        if (stageManager != null && !stageManager.IsLastStage())
        {
            OnStageClear();
            return;
        }
        isGameOver = true;
        SetPhase(GamePhase.GameOver);
        if (OnGameOver != null) OnGameOver(Team.Player);
    }

    private void HandleDefeat()
    {
        isGameOver = true;
        SetPhase(GamePhase.GameOver);
        if (OnGameOver != null) OnGameOver(Team.Enemy);
    }

    private void OnStageClear()
    {
        StopAllCoroutines();
        isTurnProcessing = false;
        if (InputManager.Instance != null)
            InputManager.Instance.ClearSelection();

        stageManager.AdvanceStage();

        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.ClearLog();

        SetupStageBoard();
        ShowPieceSelection();
    }

    // 成りチェック: 移動後に成りゾーンにいれば成る
    public void CheckPromotion(PieceInstance piece)
    {
        if (piece == null || !piece.isAlive || piece.isPromoted || !piece.data.canPromote) return;
        if (piece.CanPromoteAt(piece.boardPosition.y, boardManager.CurrentBoardSize))
            PromotePiece(piece);
    }

    /// <summary>駒を成らせ、成りに伴う特殊処理（過労死・提督化）を行う</summary>
    public void PromotePiece(PieceInstance piece)
    {
        if (piece == null || !piece.isAlive || piece.isPromoted || !piece.data.canPromote) return;

        piece.Promote();
        CombatResolver.PlayFlip(piece);
        FloatingText.Spawn(piece.boardPosition, "成", Palette.GoldLight, 4f);

        // 過労死チェック（SN・小錦）
        if (piece.data.diesOnPromotion)
        {
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(piece.DisplayName, piece.team) + " は過労死した...");
            CombatResolver.KillPiece(piece);
            return;
        }

        // 物鉄→提督の特殊処理（ワープ+召喚）
        if (piece.data.pieceType == PieceType.Monotetsu && AbilitySystem.Instance != null)
            AbilitySystem.Instance.ExecuteTeitokuPromotion(piece);
    }

    public void Resign()
    {
        if (isGameOver) return;
        StopAllCoroutines();
        isTurnProcessing = false;
        HandleDefeat();
    }

    public void SetPhase(GamePhase phase)
    {
        currentPhase = phase;
        if (OnPhaseChanged != null) OnPhaseChanged(phase);
    }
}
