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

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        boardRenderer = GetComponent<BoardRenderer>();

        // Auto-load additional PieceData from Resources/Pieces/
        PieceData[] resourcePieces = Resources.LoadAll<PieceData>("Pieces");
        if (resourcePieces != null && resourcePieces.Length > 0)
        {
            List<PieceData> combined = new List<PieceData>();
            if (allPieceData != null)
            {
                for (int i = 0; i < allPieceData.Length; i++)
                    combined.Add(allPieceData[i]);
            }
            for (int i = 0; i < resourcePieces.Length; i++)
            {
                bool duplicate = false;
                for (int j = 0; j < combined.Count; j++)
                {
                    if (combined[j] != null && combined[j].pieceType == resourcePieces[i].pieceType)
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

        boardRenderer.BuildBoard(size);
        BuildPlacementOrder();

        // カメラ調整
        float center = (size - 1) * 0.5f;
        Camera.main.transform.position = new Vector3(center, center, -10);
        Camera.main.orthographicSize = size * 0.45f + 1.5f;
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

    public PieceController SpawnPiece(PieceData data, Team team, Vector2Int pos)
    {
        if (!IsInBounds(pos) || board[pos.x, pos.y] != null) return null;

        var instance = new PieceInstance(data, team, pos);
        PlacePiece(instance, pos);

        GameObject obj = new GameObject(team + "_" + data.displayName + "_" + pos.x + "_" + pos.y);
        obj.transform.position = new Vector3(pos.x, pos.y, 0);

        PieceRenderer pr = obj.AddComponent<PieceRenderer>();
        pr.Init(instance);

        PieceController pc = obj.AddComponent<PieceController>();
        pc.Init(instance);

        pieceObjects[pos] = pc;
        return pc;
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

    public void RemovePieceController(Vector2Int pos)
    {
        PieceController pc;
        if (pieceObjects.TryGetValue(pos, out pc))
        {
            pieceObjects.Remove(pos);
            if (pc != null) pc.DestroyPiece();
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

    public void RemovePiece(Vector2Int pos)
    {
        var piece = board[pos.x, pos.y];
        if (piece != null)
        {
            piece.isAlive = false;
            allPieces.Remove(piece);
            board[pos.x, pos.y] = null;

            // 髑髏の死亡時爆発フック
            if (piece.data.pieceType == PieceType.Dokuro && AbilitySystem.Instance != null)
                AbilitySystem.Instance.OnPieceDeath(piece, pos);
        }
        else
        {
            board[pos.x, pos.y] = null;
        }
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
        foreach (var p in allPieces)
            if (p.team == team && p.isAlive) result.Add(p);
        return result;
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

    // 外側から内側への配置順でプレイヤー駒の空きスロットを見つける
    public Vector2Int? FindEmptySlotOuterFirst(Team team)
    {
        if (team == Team.Player)
        {
            foreach (var pos in playerPlacementOrder)
            {
                if (IsInBounds(pos) && board[pos.x, pos.y] == null)
                    return pos;
            }
        }
        else
        {
            // 敵側は上から
            for (int y = boardSize - 1; y >= boardSize - 3; y--)
            {
                for (int x = 0; x < boardSize; x++)
                {
                    if (IsInBounds(new Vector2Int(x, y)) && board[x, y] == null)
                        return new Vector2Int(x, y);
                }
            }
        }
        return null;
    }

    public void ClearTeam(Team team)
    {
        var toRemove = new List<Vector2Int>();
        for (int x = 0; x < boardSize; x++)
            for (int y = 0; y < boardSize; y++)
            {
                var p = board[x, y];
                if (p != null && p.team == team)
                    toRemove.Add(new Vector2Int(x, y));
            }
        foreach (var pos in toRemove)
        {
            RemovePieceController(pos);
            RemovePiece(pos);
        }
    }

    public void ClearAll()
    {
        var toRemove = new List<Vector2Int>();
        for (int x = 0; x < boardSize; x++)
            for (int y = 0; y < boardSize; y++)
                if (board[x, y] != null) toRemove.Add(new Vector2Int(x, y));
        foreach (var pos in toRemove)
        {
            RemovePieceController(pos);
            RemovePiece(pos);
        }
    }

    // プレイヤー駒を初期位置にリセット(HPも全回復)
    public void ResetPlayerPiecesToInitialPositions()
    {
        var playerPieces = GetTeamPieces(Team.Player);
        var pieceDataList = new List<PieceData>();

        // 現在の駒データを保存してからクリア
        foreach (var p in playerPieces)
            pieceDataList.Add(p.data);
        ClearTeam(Team.Player);

        // C3を中央下に再配置
        var c3Data = GetPieceDataByType(PieceType.C3);
        if (c3Data != null)
        {
            SpawnPiece(c3Data, Team.Player, new Vector2Int(boardSize / 2, 0));
            pieceDataList.Remove(c3Data);
        }

        // 残りの駒を外側から配置
        foreach (var data in pieceDataList)
        {
            var slot = FindEmptySlotOuterFirst(Team.Player);
            if (slot.HasValue)
                SpawnPiece(data, Team.Player, slot.Value);
        }
    }
}
