using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// RPG将棋用AI - Alpha-Beta探索 + RPG戦闘理解型評価関数
///
/// 設計方針:
/// - 0ダメージ攻撃は絶対にしない（C3/挑発駒以外）
/// - C3の安全を最優先で考慮
/// - 有利なトレードのみ攻撃する
/// - 危険なマスへの移動を避ける
/// - 前進しつつもC3を守る
/// </summary>
public class SimpleAI : MonoBehaviour
{
    public static SimpleAI Instance { get; private set; }

    public float moveDelay = 0.5f;

    // 探索設定: depth 3 = AI手→プレイヤー応手→AI手
    private const int SEARCH_DEPTH = 3;
    // 各深さで検討する手の上限（性能と品質のバランス）
    private const int MAX_MOVES = 25;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ================================================================
    // イベント登録
    // ================================================================
    private bool isSubscribed;

    void OnEnable() { TrySubscribe(); }

    void OnDisable()
    {
        if (isSubscribed && GameManager.Instance != null)
        {
            GameManager.Instance.OnTurnChanged -= OnTurnChanged;
            isSubscribed = false;
        }
    }

    void Start() { TrySubscribe(); }

    void Update()
    {
        if (!isSubscribed) TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (!isSubscribed && GameManager.Instance != null)
        {
            GameManager.Instance.OnTurnChanged += OnTurnChanged;
            isSubscribed = true;
        }
    }

    private void OnTurnChanged(Team team)
    {
        if (team == Team.Enemy)
            StartCoroutine(DoAITurn());
    }

    // ================================================================
    // AIターン: 候補生成→探索→最善手実行
    // ================================================================
    private IEnumerator DoAITurn()
    {
        yield return new WaitForSeconds(moveDelay);

        BoardManager bm = BoardManager.Instance;
        PieceInstance bestPiece = null;
        MoveValidator.MoveResult bestMove = default(MoveValidator.MoveResult);
        int bestScore = int.MinValue + 1;

        var candidates = GenerateOrderedMoves(Team.Enemy);

        // === 即勝ちチェック: プレイヤーC3を倒せるなら即実行 ===
        PieceInstance playerC3 = bm.FindC3(Team.Player);
        if (playerC3 != null)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!candidates[i].move.isAttack) continue;
                if (candidates[i].move.position.x != playerC3.boardPosition.x
                    || candidates[i].move.position.y != playerC3.boardPosition.y) continue;
                int dmg = Mathf.Max(0, candidates[i].piece.ATK - playerC3.DEF);
                if (dmg >= playerC3.currentHP)
                {
                    ExecuteAIMove(candidates[i].piece, candidates[i].move);
                    GameManager.Instance.EndTurn();
                    yield break;
                }
            }
        }

        // === 挑発駒を探す（タゲ強化用） ===
        PieceInstance tauntTarget = null;
        List<PieceInstance> pList = bm.GetTeamPieces(Team.Player);
        for (int t = 0; t < pList.Count; t++)
        {
            if (pList[t].data.isTauntPiece && pList[t].isAlive)
            {
                tauntTarget = pList[t];
                break;
            }
        }

        // === Alpha-Beta探索で最善手を選ぶ ===
        int searchCount = Mathf.Min(candidates.Count, MAX_MOVES);

        for (int i = 0; i < searchCount; i++)
        {
            MoveUndo undo = SimulateMove(candidates[i].piece, candidates[i].move);
            int score = AlphaBeta(SEARCH_DEPTH - 1, false, int.MinValue + 1, int.MaxValue - 1);
            RestoreMove(undo);

            // 挑発駒タゲ強化: C3に届かない駒が挑発駒を攻撃→大幅ボーナス
            if (tauntTarget != null && candidates[i].move.isAttack)
            {
                PieceInstance atkTarget = bm.GetPieceAt(candidates[i].move.position);
                if (atkTarget != null && atkTarget.data.isTauntPiece)
                {
                    bool canHitC3 = playerC3 != null
                        && CanReachTarget(candidates[i].piece, playerC3.boardPosition);
                    if (!canHitC3)
                        score += 3000;
                    else
                        score += 500;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestPiece = candidates[i].piece;
                bestMove = candidates[i].move;
            }
        }

        if (bestPiece != null)
            ExecuteAIMove(bestPiece, bestMove);

        GameManager.Instance.EndTurn();
    }

    // ================================================================
    // Alpha-Beta探索 (正の値 = Enemy/AI有利)
    // ================================================================
    private int AlphaBeta(int depth, bool maximizing, int alpha, int beta)
    {
        BoardManager bm = BoardManager.Instance;
        PieceInstance pC3 = bm.FindC3(Team.Player);
        PieceInstance eC3 = bm.FindC3(Team.Enemy);

        // 終了判定: C3撃破
        if (pC3 == null) return 100000 + depth;   // AI勝ち（早いほど高得点）
        if (eC3 == null) return -100000 - depth;   // プレイヤー勝ち

        // 葉ノード: 局面評価
        if (depth <= 0)
            return Evaluate();

        Team team = maximizing ? Team.Enemy : Team.Player;
        var moves = GenerateOrderedMoves(team);
        int moveCount = Mathf.Min(moves.Count, MAX_MOVES);

        if (moveCount == 0)
            return Evaluate();

        if (maximizing)
        {
            int best = int.MinValue + 1;
            for (int i = 0; i < moveCount; i++)
            {
                MoveUndo undo = SimulateMove(moves[i].piece, moves[i].move);
                int score = AlphaBeta(depth - 1, false, alpha, beta);
                RestoreMove(undo);

                if (score > best) best = score;
                if (score > alpha) alpha = score;
                if (alpha >= beta) break;
            }
            return best;
        }
        else
        {
            int best = int.MaxValue - 1;
            for (int i = 0; i < moveCount; i++)
            {
                MoveUndo undo = SimulateMove(moves[i].piece, moves[i].move);
                int score = AlphaBeta(depth - 1, true, alpha, beta);
                RestoreMove(undo);

                if (score < best) best = score;
                if (score < beta) beta = score;
                if (alpha >= beta) break;
            }
            return best;
        }
    }

    // ================================================================
    // 局面評価関数 (正の値 = Enemy/AI有利)
    // ================================================================

    /// <summary>駒の戦闘力を数値化</summary>
    private int PieceValue(PieceInstance p)
    {
        int v = p.ATK * 30 + p.DEF * 20 + p.currentHP * 20;
        if (p.isPromoted) v += 40;
        return v;
    }

    /// <summary>駒が移動可能か</summary>
    private bool CanMove(PieceInstance p)
    {
        if (p.data.isImmovable) return false;
        if (p.isPromoted && p.data.isImmovableWhenPromoted) return false;
        if (p.data.isAutoMove) return false;
        return true;
    }

    private int Evaluate()
    {
        BoardManager bm = BoardManager.Instance;
        PieceInstance pC3 = bm.FindC3(Team.Player);
        PieceInstance eC3 = bm.FindC3(Team.Enemy);

        if (pC3 == null) return 100000;
        if (eC3 == null) return -100000;

        int score = 0;
        int boardSize = bm.CurrentBoardSize;

        List<PieceInstance> ePieces = bm.GetTeamPieces(Team.Enemy);
        List<PieceInstance> pPieces = bm.GetTeamPieces(Team.Player);

        // === 1. 戦力差 (Material) ===
        for (int i = 0; i < ePieces.Count; i++)
        {
            if (ePieces[i].data.pieceType != PieceType.C3)
                score += PieceValue(ePieces[i]);
        }
        for (int i = 0; i < pPieces.Count; i++)
        {
            if (pPieces[i].data.pieceType != PieceType.C3)
                score -= PieceValue(pPieces[i]);
        }

        // === 2. C3のHP ===
        score += eC3.currentHP * 300;
        score -= pC3.currentHP * 300;

        // HP低下ボーナス/ペナルティ
        if (pC3.currentHP <= 2) score += 2000;
        if (pC3.currentHP <= 1) score += 4000;
        if (eC3.currentHP <= 2) score -= 2000;
        if (eC3.currentHP <= 1) score -= 4000;

        // === 3. C3の護衛（隣接味方駒数） ===
        score += CountAdjacent(eC3.boardPosition, Team.Enemy) * 200;
        score -= CountAdjacent(pC3.boardPosition, Team.Player) * 200;

        // === 4. C3への脅威（到達可能な敵駒数） ===
        int threatsToPC3 = 0;
        int totalDmgToPC3 = 0;
        for (int i = 0; i < ePieces.Count; i++)
        {
            PieceInstance ep = ePieces[i];
            if (ep.data.pieceType == PieceType.C3 || !CanMove(ep)) continue;
            if (CanReachTarget(ep, pC3.boardPosition))
            {
                threatsToPC3++;
                totalDmgToPC3 += Mathf.Max(0, ep.ATK - pC3.DEF);
            }
        }

        int threatsToEC3 = 0;
        int totalDmgToEC3 = 0;
        for (int i = 0; i < pPieces.Count; i++)
        {
            PieceInstance pp = pPieces[i];
            if (pp.data.pieceType == PieceType.C3 || !CanMove(pp)) continue;
            if (CanReachTarget(pp, eC3.boardPosition))
            {
                threatsToEC3++;
                totalDmgToEC3 += Mathf.Max(0, pp.ATK - eC3.DEF);
            }
        }

        score += threatsToPC3 * 500;
        score -= threatsToEC3 * 500;
        if (threatsToPC3 >= 2) score += 1500;
        if (threatsToEC3 >= 2) score -= 1500;
        // 致死脅威ボーナス
        if (totalDmgToPC3 >= pC3.currentHP) score += 4000;
        if (totalDmgToEC3 >= eC3.currentHP) score -= 4000;

        // === 5. 駒の安全性 ===
        // AI駒が倒される危険
        for (int i = 0; i < ePieces.Count; i++)
        {
            PieceInstance ep = ePieces[i];
            if (ep.data.pieceType == PieceType.C3 || ep.data.isImmovable) continue;
            if (CanBeKilledBy(ep, pPieces))
                score -= PieceValue(ep);
        }
        // プレイヤー駒を倒せるボーナス
        for (int i = 0; i < pPieces.Count; i++)
        {
            PieceInstance pp = pPieces[i];
            if (pp.data.pieceType == PieceType.C3 || pp.data.isImmovable) continue;
            if (CanBeKilledBy(pp, ePieces))
                score += PieceValue(pp);
        }

        // === 6. 敵C3への接近 ===
        for (int i = 0; i < ePieces.Count; i++)
        {
            PieceInstance ep = ePieces[i];
            if (ep.data.pieceType == PieceType.C3 || !CanMove(ep)) continue;
            int dist = Chebyshev(ep.boardPosition, pC3.boardPosition);
            score += (10 - dist) * 4;
        }

        // === 7. 前進ボーナス ===
        int maxRow = boardSize - 1;
        for (int i = 0; i < ePieces.Count; i++)
        {
            PieceInstance ep = ePieces[i];
            if (ep.data.pieceType == PieceType.C3 || !CanMove(ep)) continue;
            score += (maxRow - ep.boardPosition.y) * 3;
        }

        // === 8. 挑発駒への脅威ボーナス ===
        for (int i = 0; i < pPieces.Count; i++)
        {
            if (!pPieces[i].data.isTauntPiece || !pPieces[i].isAlive) continue;
            Vector2Int tauntPos = pPieces[i].boardPosition;
            for (int j = 0; j < ePieces.Count; j++)
            {
                PieceInstance ep = ePieces[j];
                if (ep.data.pieceType == PieceType.C3 || !CanMove(ep)) continue;
                if (CanReachTarget(ep, tauntPos))
                {
                    score += 800;
                    if (Chebyshev(ep.boardPosition, tauntPos) <= 1)
                        score += 400;
                }
            }
            break; // 挑発駒は1体のみ想定
        }

        return score;
    }

    // ================================================================
    // 到達判定ヘルパー
    // ================================================================

    /// <summary>駒がtargetマスに到達可能か（ブロック判定込み）</summary>
    private bool CanReachTarget(PieceInstance piece, Vector2Int target)
    {
        BoardManager bm = BoardManager.Instance;
        MoveDirection[] dirs = piece.GetMoveDirections();
        for (int i = 0; i < dirs.Length; i++)
        {
            int dx = dirs[i].direction.x;
            int dy = dirs[i].direction.y;
            bool canJump = dirs[i].canJump;

            for (int d = 1; d <= dirs[i].maxDistance; d++)
            {
                int px = piece.boardPosition.x + dx * d;
                int py = piece.boardPosition.y + dy * d;
                if (px < 0 || py < 0 || px >= bm.CurrentBoardSize || py >= bm.CurrentBoardSize) break;

                if (px == target.x && py == target.y) return true;

                if (!canJump)
                {
                    PieceInstance blocker = bm.GetPieceAt(new Vector2Int(px, py));
                    if (blocker != null) break;
                }
            }
        }
        return false;
    }

    /// <summary>targetがattackersリスト内のいずれかの駒に倒されうるか</summary>
    private bool CanBeKilledBy(PieceInstance target, List<PieceInstance> attackers)
    {
        for (int i = 0; i < attackers.Count; i++)
        {
            PieceInstance atk = attackers[i];
            if (atk.data.pieceType == PieceType.C3 || !CanMove(atk)) continue;
            int dmg = Mathf.Max(0, atk.ATK - target.DEF);
            if (dmg < target.currentHP) continue;
            if (CanReachTarget(atk, target.boardPosition))
                return true;
        }
        return false;
    }

    /// <summary>隣接する指定チームの駒数を数える</summary>
    private int CountAdjacent(Vector2Int pos, Team team)
    {
        BoardManager bm = BoardManager.Instance;
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int p = new Vector2Int(pos.x + dx, pos.y + dy);
                if (!bm.IsInBounds(p)) continue;
                PieceInstance piece = bm.GetPieceAt(p);
                if (piece != null && piece.team == team)
                    count++;
            }
        }
        return count;
    }

    private int Chebyshev(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    // ================================================================
    // 候補手生成（優先度順にソート済み）
    // ================================================================
    private struct MoveEntry
    {
        public PieceInstance piece;
        public MoveValidator.MoveResult move;
        public int priority;
    }

    private List<MoveEntry> GenerateOrderedMoves(Team team)
    {
        BoardManager bm = BoardManager.Instance;
        List<PieceInstance> pieces = bm.GetTeamPieces(team);
        var entries = new List<MoveEntry>();

        Team enemyTeam = (team == Team.Enemy) ? Team.Player : Team.Enemy;
        PieceInstance targetC3 = bm.FindC3(enemyTeam);
        PieceInstance ownC3 = bm.FindC3(team);

        // 挑発駒を探す
        PieceInstance tauntPiece = null;
        List<PieceInstance> enemyPieces = bm.GetTeamPieces(enemyTeam);
        for (int i = 0; i < enemyPieces.Count; i++)
        {
            if (enemyPieces[i].data.isTauntPiece && enemyPieces[i].isAlive)
            {
                tauntPiece = enemyPieces[i];
                break;
            }
        }

        for (int p = 0; p < pieces.Count; p++)
        {
            PieceInstance piece = pieces[p];
            if (piece.data.pieceType == PieceType.C3) continue;
            if (!CanMove(piece)) continue;

            List<MoveValidator.MoveResult> moves = MoveValidator.GetValidMoves(piece);
            for (int m = 0; m < moves.Count; m++)
            {
                MoveValidator.MoveResult move = moves[m];
                int priority = 0;

                if (move.isAttack)
                {
                    PieceInstance target = bm.GetPieceAt(move.position);
                    if (target == null) continue;

                    int damage = Mathf.Max(0, piece.ATK - target.DEF);

                    // === 0ダメージ攻撃は絶対にスキップ（C3と挑発駒以外） ===
                    if (damage <= 0)
                    {
                        bool isC3 = target.data.pieceType == PieceType.C3;
                        bool isTaunt = tauntPiece != null && target == tauntPiece;
                        if (!isC3 && !isTaunt) continue;
                    }

                    bool canKill = damage >= target.currentHP;

                    if (target.data.pieceType == PieceType.C3)
                    {
                        // C3攻撃: 最高優先度
                        priority = canKill ? 50000 : (10000 + damage * 100);
                    }
                    else if (tauntPiece != null && target == tauntPiece)
                    {
                        // 挑発駒攻撃: C3に到達不可なら最優先
                        bool canHitC3 = targetC3 != null && CanReachTarget(piece, targetC3.boardPosition);
                        if (!canHitC3)
                            priority = canKill ? 49000 : (25000 + damage * 100);
                        else
                            priority = canKill ? 40000 : (9000 + damage * 50);
                    }
                    else if (canKill)
                    {
                        // 撃破可能: 相手の駒価値が高いほど優先
                        // 自駒の価値が低いほど有利なトレード
                        int victimVal = PieceValue(target);
                        int attackerVal = PieceValue(piece);
                        priority = 5000 + victimVal - attackerVal / 4;
                    }
                    else
                    {
                        // ダメージのみ（撃破不可）
                        priority = 1000 + damage * 30;
                    }
                }
                else
                {
                    // === 移動手 ===
                    // 優先攻撃対象に接近
                    Vector2Int approach;
                    if (tauntPiece != null)
                        approach = tauntPiece.boardPosition;
                    else if (targetC3 != null)
                        approach = targetC3.boardPosition;
                    else
                        approach = new Vector2Int(-1, -1);

                    if (approach.x >= 0)
                    {
                        int curDist = Chebyshev(piece.boardPosition, approach);
                        int newDist = Chebyshev(move.position, approach);
                        // 接近するほど高得点
                        priority += (curDist - newDist) * 40;
                    }

                    // 自C3の近くに守り駒を配置
                    if (ownC3 != null)
                    {
                        int distToOwn = Chebyshev(move.position, ownC3.boardPosition);
                        if (distToOwn <= 1) priority += 150;
                    }

                    // 前進ボーナス
                    if (team == Team.Enemy)
                        priority += (bm.CurrentBoardSize - 1 - move.position.y) * 2;
                    else
                        priority += move.position.y * 2;
                }

                MoveEntry entry = new MoveEntry();
                entry.piece = piece;
                entry.move = move;
                entry.priority = priority;
                entries.Add(entry);
            }
        }

        entries.Sort(delegate(MoveEntry a, MoveEntry b) { return b.priority.CompareTo(a.priority); });
        return entries;
    }

    // ================================================================
    // 手のシミュレーション（探索用: 盤面を一時的に変更）
    // ================================================================
    private struct MoveUndo
    {
        public PieceInstance piece;
        public Vector2Int from;
        public Vector2Int to;
        public PieceInstance captured;
        public int capturedHP;
        public bool wasPromoted;
        public int oldHP;
    }

    private MoveUndo SimulateMove(PieceInstance piece, MoveValidator.MoveResult move)
    {
        BoardManager bm = BoardManager.Instance;
        MoveUndo undo = new MoveUndo();
        undo.piece = piece;
        undo.from = piece.boardPosition;
        undo.to = move.position;
        undo.captured = null;
        undo.wasPromoted = piece.isPromoted;
        undo.oldHP = piece.currentHP;

        if (move.isAttack)
        {
            PieceInstance target = bm.GetPieceAt(move.position);
            if (target != null)
            {
                int damage = Mathf.Max(0, piece.ATK - target.DEF);
                undo.captured = target;
                undo.capturedHP = target.currentHP;
                target.currentHP -= damage;

                if (target.currentHP <= 0)
                {
                    // 撃破: ターゲット除去 → 攻撃者移動
                    target.isAlive = false;
                    bm.RemovePieceFromBoard(move.position);
                    bm.RemovePieceFromBoard(piece.boardPosition);
                    bm.PlacePieceOnBoard(piece, move.position);
                }
                else
                {
                    // 非撃破: 攻撃者は動かない
                    return undo;
                }
            }
        }
        else
        {
            // 通常移動
            bm.RemovePieceFromBoard(piece.boardPosition);
            bm.PlacePieceOnBoard(piece, move.position);
        }

        // 移動コスト（SN駒: 移動毎にHP-1）
        if (piece.data.losesHPOnMove)
            piece.currentHP--;

        // 成り判定
        if (!piece.isPromoted && piece.data.canPromote)
        {
            if (piece.CanPromoteAt(move.position.y, bm.CurrentBoardSize))
            {
                piece.isPromoted = true;
                if (piece.data.diesOnPromotion)
                {
                    piece.isAlive = false;
                    bm.RemovePieceFromBoard(move.position);
                }
            }
        }

        return undo;
    }

    private void RestoreMove(MoveUndo undo)
    {
        BoardManager bm = BoardManager.Instance;
        PieceInstance piece = undo.piece;

        // 状態復元
        piece.isPromoted = undo.wasPromoted;
        piece.currentHP = undo.oldHP;
        piece.isAlive = true;

        if (undo.captured != null)
        {
            // 捕獲駒のHP復元
            undo.captured.currentHP = undo.capturedHP;

            int damage = Mathf.Max(0, piece.ATK - undo.captured.DEF);
            if (undo.capturedHP <= damage)
            {
                // 撃破だった: 両駒を元の位置に戻す
                bm.RemovePieceFromBoard(undo.to);
                bm.PlacePieceOnBoard(piece, undo.from);
                undo.captured.isAlive = true;
                bm.PlacePieceOnBoard(undo.captured, undo.to);
            }
            // 非撃破: 駒は動いていないのでHP復元のみ
        }
        else
        {
            // 通常移動: 元に戻す
            if (bm.GetPieceAt(undo.to) == piece)
                bm.RemovePieceFromBoard(undo.to);
            bm.PlacePieceOnBoard(piece, undo.from);
        }
    }

    // ================================================================
    // AI手の実行（視覚エフェクト付き）
    // ================================================================
    public void ExecuteAIMove(PieceInstance piece, MoveValidator.MoveResult move)
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
                        BattleLogUI.Instance.AddLog(
                            BattleLogUI.ColorName(piece.DisplayName, piece.team) + " \u2192 " +
                            BattleLogUI.ColorName(target.DisplayName, target.team) + " \u30C0\u30E1\u30FC\u30B8\u7121\u52B9");
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
                        BattleLogUI.Instance.AddLog(
                            BattleLogUI.ColorName(piece.DisplayName, piece.team) + " \u304C " +
                            BattleLogUI.ColorName(target.DisplayName, target.team) + " \u3092\u6483\u7834");
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
                        BattleLogUI.Instance.AddLog(
                            BattleLogUI.ColorName(piece.DisplayName, piece.team) + " \u2192 " +
                            BattleLogUI.ColorName(target.DisplayName, target.team) + " " + damage + "\u30C0\u30E1\u30FC\u30B8");
                    if (targetPC != null) targetPC.Shake();
                    return;
                }
            }
        }

        PieceController pc = bm.GetPieceController(from);
        bm.MovePiece(from, to);
        bm.UpdatePieceControllerPosition(from, to);
        if (pc != null) pc.MoveTo(to);

        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayMoveEffect();

        // SN: 移動毎にHP-1
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
                    BattleLogUI.Instance.AddLog(
                        BattleLogUI.ColorName(piece.DisplayName, piece.team) + " \u306F\u529B\u5C3D\u304D\u305F...");
                return;
            }
        }

        GameManager.Instance.CheckPromotion(piece);
    }
}
