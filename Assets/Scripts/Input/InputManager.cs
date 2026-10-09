using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    private PieceInstance selectedPiece;
    private List<MoveValidator.MoveResult> currentValidMoves;

    // 敵駒クリック時の閲覧用(移動ハイライトなし)
    private PieceInstance viewedPiece;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.currentPhase != GamePhase.Battle) return;
        // 能力処理中（なこ移動等）は入力をブロック
        if (GameManager.Instance.IsTurnProcessing) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // プレイヤーターンでなくてもクリックは受け付ける(敵駒閲覧のため)
            HandleClick();
        }
    }

    private void HandleClick()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0));
        Vector2Int boardPos = new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y));

        if (!BoardManager.Instance.IsInBounds(boardPos)) return;

        bool isPlayerTurn = (GameManager.Instance.currentTurn == Team.Player);
        PieceInstance clickedPiece = BoardManager.Instance.GetPieceAt(boardPos);

        // 駒が選択済みの場合(プレイヤーターン中のみ移動可能)
        if (selectedPiece != null && isPlayerTurn)
        {
            if (boardPos == selectedPiece.boardPosition)
            {
                ClearSelection();
                return;
            }

            MoveValidator.MoveResult? targetMove = null;
            if (currentValidMoves != null)
            {
                foreach (var move in currentValidMoves)
                {
                    if (move.position == boardPos)
                    {
                        targetMove = move;
                        break;
                    }
                }
            }

            if (targetMove.HasValue)
            {
                ExecuteMove(selectedPiece, targetMove.Value);
                return;
            }

            // 自駒クリックで選択切替
            if (clickedPiece != null && clickedPiece.team == Team.Player)
            {
                SelectPiece(clickedPiece);
                return;
            }

            // 敵駒クリックで閲覧
            if (clickedPiece != null && clickedPiece.team == Team.Enemy)
            {
                ClearSelection();
                ViewPiece(clickedPiece);
                return;
            }

            ClearSelection();
            return;
        }

        // 駒未選択
        if (clickedPiece != null)
        {
            if (clickedPiece.team == Team.Player && isPlayerTurn)
            {
                SelectPiece(clickedPiece);
            }
            else
            {
                // 敵駒 or プレイヤーターン外: 閲覧のみ
                ViewPiece(clickedPiece);
            }
        }
        else
        {
            ClearSelection();
        }
    }

    private void SelectPiece(PieceInstance piece)
    {
        // 手動操作不可の駒は閲覧のみ
        if (!piece.data.isManualControllable)
        {
            ViewPiece(piece);
            return;
        }

        ClearSelection();
        selectedPiece = piece;
        viewedPiece = piece;
        currentValidMoves = MoveValidator.GetValidMoves(piece);

        HighlightManager.Instance.ShowSelectedHighlight(piece.boardPosition);
        HighlightManager.Instance.ShowMoveHighlights(currentValidMoves);
    }

    private void ViewPiece(PieceInstance piece)
    {
        // 移動ハイライトなしで閲覧のみ
        viewedPiece = piece;
        selectedPiece = null;
        currentValidMoves = null;
    }

    public void ClearSelection()
    {
        selectedPiece = null;
        viewedPiece = null;
        currentValidMoves = null;
        if (HighlightManager.Instance != null)
            HighlightManager.Instance.ClearHighlights();
    }

    private void ExecuteMove(PieceInstance piece, MoveValidator.MoveResult move)
    {
        BoardManager bm = BoardManager.Instance;
        Vector2Int from = piece.boardPosition;
        Vector2Int to = move.position;

        if (move.isAttack)
        {
            PieceInstance target = bm.GetPieceAt(to);
            if (target != null)
            {
                int damage = Mathf.Max(0, piece.ATK - target.DEF);

                // 挑発駒はダメージ無効（∞HP）
                if (target.data.isTauntPiece)
                {
                    if (BattleEffects.Instance != null)
                        BattleEffects.Instance.PlayHitEffect(to);
                    PieceController tpc = bm.GetPieceController(to);
                    if (tpc != null) tpc.Shake();
                    if (BattleLogUI.Instance != null)
                        BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(piece.DisplayName, piece.team) + " \u2192 " + BattleLogUI.ColorName(target.DisplayName, target.team) + " \u30C0\u30E1\u30FC\u30B8\u7121\u52B9");
                    ClearSelection();
                    GameManager.Instance.EndTurn();
                    return;
                }

                target.currentHP -= damage;

                PieceController targetPC = bm.GetPieceController(to);
                if (targetPC != null) targetPC.UpdateHP();

                if (target.currentHP <= 0)
                {
                    if (BattleEffects.Instance != null)
                        BattleEffects.Instance.PlayDefeatEffect(to);
                    if (BattleLogUI.Instance != null)
                        BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(piece.DisplayName, piece.team) + " が " + BattleLogUI.ColorName(target.DisplayName, target.team) + " を撃破！");
                    int groupId = target.linkedGroupId;
                    bm.RemovePiece(to);
                    bm.RemovePieceController(to);
                    if (AbilitySystem.Instance != null)
                        AbilitySystem.Instance.CheckLinkedDeaths(groupId);
                }
                else
                {
                    if (BattleEffects.Instance != null)
                        BattleEffects.Instance.PlayHitEffect(to);
                    if (BattleLogUI.Instance != null)
                        BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(piece.DisplayName, piece.team) + " → " + BattleLogUI.ColorName(target.DisplayName, target.team) + " " + damage + "ダメージ");
                    if (targetPC != null) targetPC.Shake();
                    ClearSelection();
                    GameManager.Instance.EndTurn();
                    return;
                }
            }
        }

        PieceController pc = bm.GetPieceController(from);
        bm.MovePiece(from, to);
        bm.UpdatePieceControllerPosition(from, to);
        if (pc != null) pc.MoveTo(to);

        // 移動SE
        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayMoveEffect();

        // SN: 移動でHP-1
        if (piece.data.losesHPOnMove && piece.isAlive)
        {
            piece.currentHP--;
            PieceController snPC = bm.GetPieceController(to);
            if (snPC != null) snPC.UpdateHP();
            if (piece.currentHP <= 0)
            {
                if (BattleEffects.Instance != null)
                    BattleEffects.Instance.PlayDefeatEffect(to);
                bm.RemovePiece(to);
                bm.RemovePieceController(to);
                if (BattleLogUI.Instance != null)
                    BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(piece.DisplayName, piece.team) + " は力尽きた...");
                ClearSelection();
                GameManager.Instance.EndTurn();
                return;
            }
        }

        // 成りチェック
        GameManager.Instance.CheckPromotion(piece);

        // 李白の裏返し能力
        if (piece.data.pieceType == PieceType.Rihaku && AbilitySystem.Instance != null)
            AbilitySystem.Instance.ExecuteRihakuAbility(piece);

        ClearSelection();
        GameManager.Instance.EndTurn();
    }

    public PieceInstance GetSelectedPiece()
    {
        return selectedPiece;
    }

    public PieceInstance GetViewedPiece()
    {
        return viewedPiece != null ? viewedPiece : selectedPiece;
    }
}
