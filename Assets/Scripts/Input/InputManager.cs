using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    private PieceInstance selectedPiece;
    private List<MoveValidator.MoveResult> currentValidMoves;

    // 敵駒クリック時の閲覧用(移動ハイライトなし)
    private PieceInstance viewedPiece;

    // プレイヤーの手を実行中（アニメーション等）
    private bool isExecutingMove;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    void Update()
    {
        // 倒された駒の選択・閲覧は解除する
        if (selectedPiece != null && !selectedPiece.isAlive) ClearSelection();
        if (viewedPiece != null && !viewedPiece.isAlive) viewedPiece = null;

        if (GameManager.Instance == null) return;
        if (GameManager.Instance.currentPhase != GamePhase.Battle) return;
        // 能力処理中（なこ移動等）・自分の手の実行中は入力をブロック
        if (GameManager.Instance.IsTurnProcessing || isExecutingMove) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            // UIの上でのクリックは盤面に通さない
            if (IsPointerOverUI(mousePos)) return;
            // プレイヤーターンでなくてもクリックは受け付ける(敵駒閲覧のため)
            HandleClick(mousePos);
        }
    }

    /// <summary>画面座標がUI Toolkitのクリック可能な要素の上にあるか</summary>
    private static bool IsPointerOverUI(Vector2 screenPos)
    {
        UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        for (int i = 0; i < docs.Length; i++)
        {
            VisualElement root = docs[i].rootVisualElement;
            if (root == null || root.panel == null) continue;
            IPanel panel = root.panel;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            VisualElement picked = panel.Pick(panelPos);
            if (picked != null && picked != panel.visualTree && !IsDocumentRoot(picked, docs))
                return true;
        }
        return false;
    }

    private static bool IsDocumentRoot(VisualElement element, UIDocument[] docs)
    {
        for (int i = 0; i < docs.Length; i++)
            if (docs[i].rootVisualElement == element) return true;
        return false;
    }

    private void HandleClick(Vector2 mousePos)
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0));
        Vector2Int boardPos = new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y));

        if (!BoardManager.Instance.IsInBounds(boardPos))
        {
            ClearSelection();
            return;
        }

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

            if (currentValidMoves != null)
            {
                foreach (var move in currentValidMoves)
                {
                    if (move.position == boardPos)
                    {
                        StartCoroutine(ExecutePlayerMove(selectedPiece, move));
                        return;
                    }
                }
            }
        }

        if (clickedPiece == null)
        {
            ClearSelection();
            return;
        }

        if (clickedPiece.team == Team.Player && isPlayerTurn)
            SelectPiece(clickedPiece);
        else
            ViewPiece(clickedPiece); // 敵駒 or プレイヤーターン外: 閲覧のみ
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

        if (HighlightManager.Instance != null)
        {
            HighlightManager.Instance.ShowMoveHighlights(currentValidMoves);
            HighlightManager.Instance.ShowSelectedHighlight(piece.boardPosition);
        }
    }

    private void ViewPiece(PieceInstance piece)
    {
        // 移動ハイライトなしで閲覧のみ
        ClearSelection();
        viewedPiece = piece;
    }

    public void ClearSelection()
    {
        selectedPiece = null;
        viewedPiece = null;
        currentValidMoves = null;
        if (HighlightManager.Instance != null)
            HighlightManager.Instance.ClearHighlights();
    }

    private IEnumerator ExecutePlayerMove(PieceInstance piece, MoveValidator.MoveResult move)
    {
        isExecutingMove = true;
        ClearSelection();

        yield return CombatResolver.ExecuteMove(piece, move);

        isExecutingMove = false;
        GameManager.Instance.EndTurn();
    }

    public PieceInstance GetViewedPiece()
    {
        PieceInstance p = viewedPiece != null ? viewedPiece : selectedPiece;
        return (p != null && p.isAlive) ? p : null;
    }
}
