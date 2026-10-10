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

    // UIの当たり判定用（毎フレーム検索しないようにキャッシュ）
    private UIDocument[] uiDocuments;
    private float uiDocumentsRefreshTime;

    /// <summary>プレイヤーの手を実行中か（パス・投了を受け付けない）</summary>
    public bool IsBusy { get { return isExecutingMove; } }

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

        GameManager gm = GameManager.Instance;
        if (gm == null || Mouse.current == null || gm.currentPhase != GamePhase.Battle)
        {
            SetHover(null);
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        bool overUI = IsPointerOverUI(mousePos);
        Vector2Int? boardPos = overUI ? (Vector2Int?)null : ScreenToBoard(mousePos);
        SetHover(boardPos);

        // 能力処理中（なこ移動等）・自分の手の実行中は入力をブロック
        if (gm.IsTurnProcessing || isExecutingMove) return;

        // UIの上でのクリックは盤面に通さない。プレイヤーターン外でも敵駒の閲覧はできる
        if (Mouse.current.leftButton.wasPressedThisFrame && !overUI)
            HandleClick(mousePos);
    }

    private static Vector2Int? ScreenToBoard(Vector2 screenPos)
    {
        if (Camera.main == null || BoardManager.Instance == null) return null;
        Vector3 world = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0));
        var pos = new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.y));
        return BoardManager.Instance.IsInBounds(pos) ? pos : (Vector2Int?)null;
    }

    private static void SetHover(Vector2Int? pos)
    {
        if (HighlightManager.Instance != null) HighlightManager.Instance.SetHover(pos);
    }

    /// <summary>画面座標がUI Toolkitのクリック可能な要素の上にあるか</summary>
    private bool IsPointerOverUI(Vector2 screenPos)
    {
        if (uiDocuments == null || Time.unscaledTime > uiDocumentsRefreshTime)
        {
            uiDocuments = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            uiDocumentsRefreshTime = Time.unscaledTime + 1f;
        }
        UIDocument[] docs = uiDocuments;
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

        // 再行動中は、その駒しか動かせない
        PieceInstance only = GameManager.Instance.ActiveBonusPiece;
        if (clickedPiece.team == Team.Player && isPlayerTurn && (only == null || clickedPiece == only))
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
            HighlightManager.Instance.ShowMoveHighlights(piece, currentValidMoves);
            HighlightManager.Instance.ShowSelectedHighlight(piece.boardPosition);
        }
        SetLifted(piece, true);
    }

    private static void SetLifted(PieceInstance piece, bool lifted)
    {
        if (piece == null || BoardManager.Instance == null) return;
        PieceController pc = BoardManager.Instance.GetPieceController(piece.boardPosition);
        if (pc != null) pc.SetLifted(lifted);
    }

    private void ViewPiece(PieceInstance piece)
    {
        // 移動ハイライトなしで閲覧のみ
        ClearSelection();
        viewedPiece = piece;
    }

    public void ClearSelection()
    {
        SetLifted(selectedPiece, false);
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
        // 再行動（ゾーン）: その駒だけもう一度動かせる。パスで手番を終えてもよい
        PieceInstance again = GameManager.Instance.TakeBonusMove();
        if (again != null)
        {
            SelectPiece(again);
            yield break;
        }
        GameManager.Instance.EndTurn();
    }

    public PieceInstance GetViewedPiece()
    {
        PieceInstance p = viewedPiece != null ? viewedPiece : selectedPiece;
        return (p != null && p.isAlive) ? p : null;
    }
}
