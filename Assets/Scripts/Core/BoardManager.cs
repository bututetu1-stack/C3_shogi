using UnityEngine;
using System.Collections.Generic;

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance { get; private set; }

    [Header("Piece Data")]
    public PieceData[] allPieceData;

    private int boardSize;
    private PieceInstance[,] board;
    private List<PieceInstance> allPieces = new List<PieceInstance>();
    private BoardRenderer boardRenderer;
    private Dictionary<Vector2Int, PieceController> pieceObjects = new Dictionary<Vector2Int, PieceController>();

    // 外側から内側への配置順序
    private List<Vector2Int> playerPlacementOrder = new List<Vector2Int>();

    public int CurrentBoardSize { get { return boardSize; } }

    /// <summary>盤を映すカメラの基準位置（揺れ演出の戻り先）</summary>
    public Vector3 CameraHomePosition
    {
        get
        {
            if (CameraFitter.Instance != null) return CameraFitter.Instance.HomePosition;
            float c = (boardSize - 1) * 0.5f;
            return new Vector3(c, c, -10f);
        }
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        boardRenderer = GetComponent<BoardRenderer>();

        // Resources/Pieces/ の駒データを追加読み込み（同じ駒種はシーン側を優先）
        PieceData[] resourcePieces = Resources.LoadAll<PieceData>("Pieces");
        if (resourcePieces != null && resourcePieces.Length > 0)
        {
            List<PieceData> combined = new List<PieceData>();
            if (allPieceData != null)
            {
                for (int i = 0; i < allPieceData.Length; i++)
                    if (allPieceData[i] != null) combined.Add(allPieceData[i]);
            }
            for (int i = 0; i < resourcePieces.Length; i++)
            {
                bool duplicate = false;
                for (int j = 0; j < combined.Count; j++)
                {
                    if (combined[j].pieceType == resourcePieces[i].pieceType)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                    combined.Add(resourcePieces[i]);
            }
            allPieceData = combined.ToArray();
        }
    }

    public void InitBoard(int size)
    {
        boardSize = size;
        board = new PieceInstance[size, size];
        allPieces.Clear();
        pieceObjects.Clear();

        if (HighlightManager.Instance != null)
            HighlightManager.Instance.ResetBoard();

        // 盤の描画とカメラ合わせ（自動プレイ中は描かない）
        if (!GameSim.Headless) boardRenderer.BuildBoard(size);
        BuildPlacementOrder();
    }

    private void BuildPlacementOrder()
    {
        playerPlacementOrder.Clear();
        // 外側から内側: row0左端→row0右端→row0左2→row0右2...→row1...
        for (int y = 0; y < Mathf.Min(3, boardSize); y++)
        {
            int left = 0;
            int right = boardSize - 1;
            while (left <= right)
            {
                playerPlacementOrder.Add(new Vector2Int(left, y));
                if (left != right)
                    playerPlacementOrder.Add(new Vector2Int(right, y));
                left++;
                right--;
            }
        }
    }

    public BoardRenderer GetRenderer() { return boardRenderer; }

    public PieceData GetPieceDataByType(PieceType type)
    {
        foreach (var pd in allPieceData)
            if (pd != null && pd.pieceType == type) return pd;
        return null;
    }

    /// <summary>駒を置き、見た目のオブジェクトを返す（置けなければ null。自動プレイ中は見た目を作らないので常に null）</summary>
    public PieceController SpawnPiece(PieceData data, Team team, Vector2Int pos)
    {
        return Spawn(data, team, pos) != null ? GetPieceController(pos) : null;
    }

    /// <summary>駒を置く。置けなければ null（自動プレイ中も駒そのものは返る）</summary>
    public PieceInstance Spawn(PieceData data, Team team, Vector2Int pos)
    {
        if (data == null)
        {
            Debug.LogWarning("SpawnPiece: PieceData が null です (" + team + " " + pos + ")");
            return null;
        }
        if (!IsInBounds(pos))
        {
            Debug.LogWarning("SpawnPiece: 盤外です " + data.displayName + " " + pos);
            return null;
        }
        if (board[pos.x, pos.y] != null)
        {
            Debug.LogWarning("SpawnPiece: マスが埋まっています " + data.displayName + " " + pos
                + " (既存: " + board[pos.x, pos.y].DisplayName + ")");
            return null;
        }

        var instance = new PieceInstance(data, team, pos);
        PlacePiece(instance, pos);
        if (GameSim.Headless) return instance;

        GameObject obj = new GameObject(team + "_" + data.displayName + "_" + pos.x + "_" + pos.y);
        obj.transform.position = new Vector3(pos.x, pos.y, 0);

        PieceRenderer pr = obj.AddComponent<PieceRenderer>();
        pr.Init(instance);

        PieceController pc = obj.AddComponent<PieceController>();
        pc.Init(instance);

        pieceObjects[pos] = pc;
        return instance;
    }

    public PieceController GetPieceController(Vector2Int pos)
    {
        PieceController pc;
        pieceObjects.TryGetValue(pos, out pc);
        return pc;
    }

    public void UpdatePieceControllerPosition(Vector2Int from, Vector2Int to)
    {
        PieceController pc;
        if (pieceObjects.TryGetValue(from, out pc))
        {
            pieceObjects.Remove(from);
            pieceObjects[to] = pc;
        }
    }

    /// <summary>見た目のオブジェクトを盤の管理から外して返す（破棄は呼び出し側で）</summary>
    public PieceController DetachPieceController(Vector2Int pos)
    {
        PieceController pc;
        if (pieceObjects.TryGetValue(pos, out pc))
            pieceObjects.Remove(pos);
        return pc;
    }

    public void RemovePieceController(Vector2Int pos, bool animate = true)
    {
        PieceController pc;
        if (pieceObjects.TryGetValue(pos, out pc))
        {
            pieceObjects.Remove(pos);
            if (pc != null) pc.DestroyPiece(animate);
        }
    }

    public PieceInstance GetPieceAt(Vector2Int pos)
    {
        if (!IsInBounds(pos)) return null;
        return board[pos.x, pos.y];
    }

    public bool IsInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < boardSize && pos.y >= 0 && pos.y < boardSize;
    }

    public bool IsEmpty(Vector2Int pos)
    {
        return IsInBounds(pos) && board[pos.x, pos.y] == null;
    }

    public void PlacePiece(PieceInstance piece, Vector2Int pos)
    {
        board[pos.x, pos.y] = piece;
        piece.boardPosition = pos;
        if (!allPieces.Contains(piece))
            allPieces.Add(piece);
    }

    // AI用: 盤面配列のみ操作（ビジュアルなし）
    public void PlacePieceOnBoard(PieceInstance piece, Vector2Int pos)
    {
        board[pos.x, pos.y] = piece;
        piece.boardPosition = pos;
    }

    // AI用: 盤面配列のみ操作（ビジュアルなし）
    public void RemovePieceFromBoard(Vector2Int pos)
    {
        board[pos.x, pos.y] = null;
    }

    /// <summary>
    /// 盤面データから駒を取り除く。triggerDeathEffects=trueなら死亡時能力（髑髏の爆発）を発動する。
    /// </summary>
    public void RemovePiece(Vector2Int pos, bool triggerDeathEffects = true)
    {
        var piece = board[pos.x, pos.y];
        board[pos.x, pos.y] = null;
        if (piece == null) return;

        piece.isAlive = false;
        allPieces.Remove(piece);

        // 髑髏の死亡時爆発フック
        if (triggerDeathEffects && piece.data.pieceType == PieceType.Dokuro && AbilitySystem.Instance != null)
            AbilitySystem.Instance.OnPieceDeath(piece, pos);
    }

    public void MovePiece(Vector2Int from, Vector2Int to)
    {
        var piece = board[from.x, from.y];
        board[from.x, from.y] = null;
        board[to.x, to.y] = piece;
        if (piece != null) piece.boardPosition = to;
    }

    public List<PieceInstance> GetTeamPieces(Team team)
    {
        var result = new List<PieceInstance>();
        GetTeamPieces(team, result);
        return result;
    }

    /// <summary>生きている駒を result に詰める（result は先にクリアされる）</summary>
    public void GetTeamPieces(Team team, List<PieceInstance> result)
    {
        result.Clear();
        for (int i = 0; i < allPieces.Count; i++)
        {
            PieceInstance p = allPieces[i];
            if (p.team == team && p.isAlive) result.Add(p);
        }
    }

    public PieceInstance FindC3(Team team)
    {
        foreach (var p in allPieces)
            if (p.team == team && p.data.pieceType == PieceType.C3 && p.isAlive) return p;
        return null;
    }

    public List<PieceData> GetPiecesOnBoard()
    {
        var result = new List<PieceData>();
        foreach (var p in allPieces)
            if (p.isAlive && !result.Contains(p.data)) result.Add(p.data);
        return result;
    }

    /// <summary>プレイヤー陣（下3段）の空きマスを外側から探す</summary>
    public Vector2Int? FindPlayerDeploySlot()
    {
        foreach (var pos in playerPlacementOrder)
        {
            if (IsInBounds(pos) && board[pos.x, pos.y] == null)
                return pos;
        }
        return null;
    }

    /// <summary>盤上の全駒を除去する（ステージ切替用。死亡時能力は発動しない）</summary>
    public void ClearAll()
    {
        if (board == null) return;
        var toRemove = new List<Vector2Int>();
        for (int x = 0; x < boardSize; x++)
            for (int y = 0; y < boardSize; y++)
                if (board[x, y] != null) toRemove.Add(new Vector2Int(x, y));
        foreach (var pos in toRemove)
        {
            RemovePieceController(pos, false);
            RemovePiece(pos, false);
        }
    }
}
