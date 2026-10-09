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

    private BoardManager boardManager;
    private PieceSelectionUI pieceSelectionUI;
    private StageManager stageManager;
    private bool isGameOver;
    public bool IsTurnProcessing { get { return isTurnProcessing; } }
    private bool isTurnProcessing;

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
        pieceSelectionUI = FindObjectOfType<PieceSelectionUI>();

        StartNewGame();
    }

    private void StartNewGame()
    {
        isGameOver = false;
        currentTurn = Team.Player;
        playerOwnedPieces.Clear();

        // ステージ1は5x5
        int size = stageManager != null ? stageManager.GetBoardSize() : 5;
        boardManager.InitBoard(size);

        // C3を中央下に配置
        var c3 = boardManager.GetPieceDataByType(PieceType.C3);
        if (c3 != null)
            boardManager.SpawnPiece(c3, Team.Player, new Vector2Int(size / 2, 0));

        // 歩兵を配置（9x9なら3段目、それ以外は2段目）
        var pawn = boardManager.GetPieceDataByType(PieceType.Pawn);
        if (pawn != null)
        {
            int pawnRow = (size >= 9) ? 2 : 1;
            for (int x = 0; x < size; x++)
                boardManager.SpawnPiece(pawn, Team.Player, new Vector2Int(x, pawnRow));
        }

        ShowPieceSelection();
    }

    private void ShowPieceSelection()
    {
        SetPhase(GamePhase.PieceSelection);

        List<PieceData> onBoard = boardManager.GetPiecesOnBoard();
        List<PieceData> choices = PiecePool.DrawPieces(boardManager.allPieceData, 3, onBoard);

        if (choices.Count == 0 || pieceSelectionUI == null)
        {
            StartBattle();
            return;
        }

        pieceSelectionUI.ShowSelection(choices, OnPieceChosen);
    }

    private void OnPieceChosen(PieceData chosen)
    {
        playerOwnedPieces.Add(chosen);

        Vector2Int? slot = boardManager.FindEmptySlotOuterFirst(Team.Player);
        if (slot.HasValue)
            boardManager.SpawnPiece(chosen, Team.Player, slot.Value);

        StartBattle();
    }

    private void StartBattle()
    {
        if (stageManager != null)
            stageManager.SetupEnemyForStage(stageManager.currentStage);

        // プレイヤー駒にもステージに応じた強化（ステージ2以降）
        if (stageManager != null && stageManager.currentStage > 1)
            stageManager.ApplyPlayerScaling();

        SetPhase(GamePhase.Battle);
        currentTurn = Team.Player;

        // ステージタイトルカード表示
        if (stageManager != null)
        {
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
    }

    public void EndTurn()
    {
        if (isGameOver) return;
        if (isTurnProcessing) return;
        if (CheckGameOver()) return;
        StartCoroutine(EndTurnSequence());
    }

    private IEnumerator EndTurnSequence()
    {
        isTurnProcessing = true;

        // ターン終了時能力（中華回復消滅・深海攻撃・艦娘攻撃）
        if (AbilitySystem.Instance != null)
            yield return AbilitySystem.Instance.ExecuteTurnEndAbilities(currentTurn);

        if (isGameOver) { isTurnProcessing = false; yield break; }
        if (CheckGameOver()) { isTurnProcessing = false; yield break; }

        if (currentTurn == Team.Player)
            currentTurn = Team.Enemy;
        else
            currentTurn = Team.Player;

        // ターン開始時能力（門人自動移動・ヲツ中華生成）を実行してからOnTurnChanged
        yield return ExecuteTurnStartThenNotify(currentTurn);
        isTurnProcessing = false;
    }

    private IEnumerator ExecuteTurnStartThenNotify(Team team)
    {
        if (AbilitySystem.Instance != null)
            yield return AbilitySystem.Instance.ExecuteTurnStartAbilities(team);

        if (isGameOver) yield break;
        if (CheckGameOver()) yield break;

        if (OnTurnChanged != null) OnTurnChanged(currentTurn);
    }

    private bool CheckGameOver()
    {
        PieceInstance playerC3 = boardManager.FindC3(Team.Player);
        PieceInstance enemyC3 = boardManager.FindC3(Team.Enemy);

        // 敵C3撃破 → ステージクリアまたは勝利
        if (enemyC3 == null)
        {
            if (stageManager != null && !stageManager.IsLastStage())
            {
                OnStageClear();
                return true;
            }
            isGameOver = true;
            SetPhase(GamePhase.GameOver);
            if (OnGameOver != null) OnGameOver(Team.Player);
            return true;
        }

        // 敵がC3以外に動ける駒がいない → ステージクリア扱い（全駒勝利）
        if (HasNoMovablePieces(Team.Enemy))
        {
            if (stageManager != null && !stageManager.IsLastStage())
            {
                OnStageClear();
                return true;
            }
            isGameOver = true;
            SetPhase(GamePhase.GameOver);
            if (OnGameOver != null) OnGameOver(Team.Player);
            return true;
        }

        // プレイヤーC3撃破 → 敗北
        if (playerC3 == null)
        {
            isGameOver = true;
            SetPhase(GamePhase.GameOver);
            if (OnGameOver != null) OnGameOver(Team.Enemy);
            return true;
        }

        // C3以外に動ける駒がいない場合 → 敗北
        if (HasNoMovablePieces(Team.Player))
        {
            isGameOver = true;
            SetPhase(GamePhase.GameOver);
            if (OnGameOver != null) OnGameOver(Team.Enemy);
            return true;
        }

        return false;
    }

    private bool HasNoMovablePieces(Team team)
    {
        var pieces = boardManager.GetTeamPieces(team);
        foreach (var piece in pieces)
        {
            if (piece.data.pieceType == PieceType.C3) continue;
            var moves = MoveValidator.GetValidMoves(piece);
            if (moves.Count > 0) return false;
        }
        return true;
    }

    private void OnStageClear()
    {
        stageManager.AdvanceStage();

        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.ClearLog();

        // 新しい盤サイズで再構築
        int newSize = stageManager.GetBoardSize();
        boardManager.ClearAll();
        boardManager.InitBoard(newSize);

        // C3を中央下に再配置
        var c3 = boardManager.GetPieceDataByType(PieceType.C3);
        if (c3 != null)
            boardManager.SpawnPiece(c3, Team.Player, new Vector2Int(newSize / 2, 0));

        // 歩兵を配置（9x9なら3段目、それ以外は2段目）
        var pawn = boardManager.GetPieceDataByType(PieceType.Pawn);
        if (pawn != null)
        {
            int pawnRow = (newSize >= 9) ? 2 : 1;
            for (int x = 0; x < newSize; x++)
                boardManager.SpawnPiece(pawn, Team.Player, new Vector2Int(x, pawnRow));
        }

        // 持ち駒を外側から再配置
        foreach (var data in playerOwnedPieces)
        {
            var slot = boardManager.FindEmptySlotOuterFirst(Team.Player);
            if (slot.HasValue)
                boardManager.SpawnPiece(data, Team.Player, slot.Value);
        }

        ShowPieceSelection();
    }

    // 成りチェック: 移動後に成りゾーンにいれば成る
    public void CheckPromotion(PieceInstance piece)
    {
        if (piece == null || !piece.isAlive || piece.isPromoted || !piece.data.canPromote) return;
        if (piece.CanPromoteAt(piece.boardPosition.y, boardManager.CurrentBoardSize))
        {
            piece.Promote();
            PieceController pc = boardManager.GetPieceController(piece.boardPosition);
            if (pc != null) pc.GetRenderer().UpdateAllStats();

            // 過労死チェック（SN）
            if (piece.data.diesOnPromotion)
            {
                if (BattleEffects.Instance != null)
                    BattleEffects.Instance.PlayDefeatEffect(piece.boardPosition);
                if (BattleLogUI.Instance != null)
                    BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(piece.DisplayName, piece.team) + " は過労死した...");
                boardManager.RemovePiece(piece.boardPosition);
                boardManager.RemovePieceController(piece.boardPosition);
                return;
            }

            // 物鉄→提督の特殊処理（ワープ+召喚）
            if (piece.data.pieceType == PieceType.Monotetsu && AbilitySystem.Instance != null)
                AbilitySystem.Instance.ExecuteTeitokuPromotion(piece);
        }
    }

    public void Resign()
    {
        isGameOver = true;
        SetPhase(GamePhase.GameOver);
        if (OnGameOver != null) OnGameOver(Team.Enemy);
    }

    public void SetPhase(GamePhase phase)
    {
        currentPhase = phase;
        if (OnPhaseChanged != null) OnPhaseChanged(phase);
    }
}
