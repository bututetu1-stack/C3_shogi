using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// RPG将棋用AI - 反復深化 Alpha-Beta 探索 + RPG戦闘理解型評価関数
///
/// 設計方針:
/// - 効果のない攻撃（0ダメージ）はしない。挑発（小錦）の制限は MoveValidator が掛ける
/// - 自分の手 → 相手の応手まで読んでから判断する（偶数手で評価して楽観しすぎない）
/// - 髑髏の爆発・SNの消耗・成り・過労死を探索の中でも再現する
/// - 時間予算を超えたら打ち切り、そこまでの最善手を指す（フリーズしない）
/// - ほぼ同点の手からはランダムに選び、毎回同じ展開にならないようにする
/// </summary>
public class SimpleAI : MonoBehaviour
{
    public static SimpleAI Instance { get; private set; }

    [Tooltip("手番が来てから指すまでの待ち時間（秒）")]
    public float moveDelay = 0.5f;
    [Tooltip("1手の思考に使う時間の上限（ミリ秒）")]
    public float thinkBudgetMs = 150f;
    [Tooltip("探索の統計をコンソールに出す")]
    public bool logSearchStats;

    // 反復深化で試す深さ（自分の手→相手の応手 で1組）
    private static readonly int[] ShallowDepths = { 2 };
    private static readonly int[] FullDepths = { 2, 4 };
    // 各ノードで読む手の上限（ルートは全手）
    private const int InnerMaxMoves = 14;

    // ステージに応じた強さ（序盤は浅く読み、悪手もそこそこ指す）
    private int[] searchDepths = FullDepths;
    // この点差以内の手は同じくらい良いとみなしてランダムに選ぶ
    private int randomMargin = 25;

    private void ConfigureDifficulty(Team team)
    {
        // 自動プレイで自軍を指すときは、いちばん強い設定で指す
        if (team == Team.Player) { searchDepths = FullDepths; randomMargin = 15; return; }
        int stage = StageManager.Instance != null ? StageManager.Instance.currentStage : 5;
        if (stage <= 2) { searchDepths = ShallowDepths; randomMargin = 140; }
        else if (stage <= 4) { searchDepths = ShallowDepths; randomMargin = 60; }
        else if (stage <= 9) { searchDepths = FullDepths; randomMargin = 30; }
        else { searchDepths = FullDepths; randomMargin = 15; }
    }

    private const int WinScore = 100000;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

    // ================================================================
    // イベント登録
    // ================================================================
    private bool isSubscribed;

    void OnEnable() { TrySubscribe(); }

    void OnDisable()
    {
        if (isSubscribed && GameManager.Instance != null)
            GameManager.Instance.OnTurnChanged -= OnTurnChanged;
        isSubscribed = false;
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
    // AIターン
    // ================================================================
    private IEnumerator DoAITurn()
    {
        yield return new WaitForSeconds(moveDelay);

        // 待機中にステージが切り替わった・決着した場合は何もしない
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.currentPhase != GamePhase.Battle || gm.currentTurn != Team.Enemy)
            yield break;

        MoveEntry? choice = ChooseMove(Team.Enemy, thinkBudgetMs);
        if (choice.HasValue)
            yield return CombatResolver.ExecuteMove(choice.Value.piece, choice.Value.move);
        else if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("相手", Team.Enemy) + " はパスした");

        gm.EndTurn();
    }

    // ================================================================
    // 探索
    // ================================================================
    private struct MoveEntry
    {
        public PieceInstance piece;
        public MoveValidator.MoveResult move;
        public int priority;
        public int score;
    }

    private System.Diagnostics.Stopwatch clock;
    private bool timeUp;
    private int nodes;
    private float budgetMs;

    /// <summary>直前の手で読み切れた深さ（2 = 自分の手と相手の応手、4 = その先まで）。統計用</summary>
    public int LastCompletedDepth { get; private set; }

    // 深さごとに使い回すバッファ
    private readonly List<List<MoveEntry>> moveBuffers = new List<List<MoveEntry>>();
    private readonly List<List<PieceInstance>> pieceBuffers = new List<List<PieceInstance>>();
    private readonly List<MoveValidator.MoveResult> moveScratch = new List<MoveValidator.MoveResult>();

    /// <summary>
    /// 自動プレイ用: team の手を1つ選ぶ（指せる手がなければ false＝パス）。
    /// 敵は本番と同じ強さ設定、自軍はいちばん強い設定で読む。budget は1手の思考時間（ミリ秒）
    /// </summary>
    public bool TryChooseMove(Team team, float budget, out PieceInstance piece, out MoveValidator.MoveResult move)
    {
        MoveEntry? choice = ChooseMove(team, budget);
        piece = choice.HasValue ? choice.Value.piece : null;
        move = choice.HasValue ? choice.Value.move : default(MoveValidator.MoveResult);
        return choice.HasValue;
    }

    private MoveEntry? ChooseMove(Team team, float budget)
    {
        clock = System.Diagnostics.Stopwatch.StartNew();
        timeUp = false;
        nodes = 0;
        budgetMs = budget;
        ConfigureDifficulty(team);
        bool forEnemy = team == Team.Enemy;
        Team opponent = forEnemy ? Team.Player : Team.Enemy;

        BoardManager bm = BoardManager.Instance;
        List<MoveEntry> root = new List<MoveEntry>();
        GenerateOrderedMoves(team, root, GetPieceBuffer(0));
        if (root.Count == 0) return null;

        // 即勝ち: 相手のC3を倒せるなら迷わず指す
        PieceInstance targetC3 = bm.FindC3(opponent);
        if (targetC3 != null)
        {
            foreach (var e in root)
            {
                if (e.move.isAttack && e.move.position == targetC3.boardPosition
                    && CombatResolver.CalcDamage(e.piece, targetC3) >= targetC3.currentHP)
                    return e;
            }
        }

        int completedDepth = 0;
        List<MoveEntry> best = null;

        foreach (int depth in searchDepths)
        {
            var scored = new List<MoveEntry>(root.Count);
            // 評価値は「敵が有利なほど正」。自軍の手番では小さいほど良い
            int bestRaw = forEnemy ? -WinScore * 2 : WinScore * 2;
            for (int i = 0; i < root.Count; i++)
            {
                MoveEntry e = root[i];
                int mark = Simulate(e.piece, e.move);
                // 最善候補との差を見るため、最善より少し悪い手までは正確に読む
                int raw = forEnemy
                    ? AlphaBeta(depth - 1, false, bestRaw - randomMargin, WinScore * 2, 1)
                    : AlphaBeta(depth - 1, true, -WinScore * 2, bestRaw + randomMargin, 1);
                Restore(mark);
                if (timeUp) break;
                e.score = forEnemy ? raw : -raw;   // 手番側から見た点
                scored.Add(e);
                if (forEnemy ? raw > bestRaw : raw < bestRaw) bestRaw = raw;
            }

            if (scored.Count > 0 && (!timeUp || best == null || scored.Count >= root.Count / 2))
            {
                scored.Sort((a, b) => b.score.CompareTo(a.score));
                best = scored;
                completedDepth = depth;
                // 次の深さは良かった手から読む
                if (!timeUp)
                {
                    var reordered = new List<MoveEntry>(scored);
                    foreach (var e in root) if (!reordered.Exists(x => SameMove(x, e))) reordered.Add(e);
                    root = reordered;
                }
            }
            if (timeUp) break;
        }

        LastCompletedDepth = completedDepth;
        if (best == null || best.Count == 0) return root[0];

        // ほぼ同点の手からランダムに選ぶ
        int top = best[0].score;
        int count = 1;
        while (count < best.Count && best[count].score >= top - randomMargin) count++;
        MoveEntry chosen = best[Random.Range(0, count)];

        if (logSearchStats)
            Debug.Log("[AI] depth=" + completedDepth + " nodes=" + nodes + " time=" + clock.ElapsedMilliseconds + "ms"
                + " candidates=" + best.Count + "/" + root.Count + " best=" + top + " ties=" + count
                + " -> " + chosen.piece.DisplayName + " " + chosen.move.position);
        return chosen;
    }

    private static bool SameMove(MoveEntry a, MoveEntry b)
    {
        return a.piece == b.piece && a.move.position == b.move.position;
    }

    /// <summary>Alpha-Beta探索（正の値 = AI有利）</summary>
    private int AlphaBeta(int depth, bool maximizing, int alpha, int beta, int ply)
    {
        nodes++;
        if ((nodes & 63) == 0 && clock.Elapsed.TotalMilliseconds > budgetMs) timeUp = true;
        if (timeUp) return 0;

        BoardManager bm = BoardManager.Instance;
        // 決着（早い勝ちほど高く、遅い負けほどまし）
        if (bm.FindC3(Team.Player) == null) return WinScore - ply;
        if (bm.FindC3(Team.Enemy) == null) return -WinScore + ply;

        if (depth <= 0)
            return Evaluate(maximizing ? Team.Enemy : Team.Player);

        List<MoveEntry> moves = GetBuffer(ply);
        GenerateOrderedMoves(maximizing ? Team.Enemy : Team.Player, moves, GetPieceBuffer(ply));
        int moveCount = Mathf.Min(moves.Count, InnerMaxMoves);

        // 動ける手がなければパスとして相手番へ
        if (moveCount == 0)
            return AlphaBeta(depth - 1, !maximizing, alpha, beta, ply + 1);

        if (maximizing)
        {
            int best = -WinScore * 2;
            for (int i = 0; i < moveCount; i++)
            {
                int mark = Simulate(moves[i].piece, moves[i].move);
                int score = AlphaBeta(depth - 1, false, alpha, beta, ply + 1);
                Restore(mark);
                if (timeUp) return 0;
                if (score > best) best = score;
                if (score > alpha) alpha = score;
                if (alpha >= beta) break;
            }
            return best;
        }
        else
        {
            int best = WinScore * 2;
            for (int i = 0; i < moveCount; i++)
            {
                int mark = Simulate(moves[i].piece, moves[i].move);
                int score = AlphaBeta(depth - 1, true, alpha, beta, ply + 1);
                Restore(mark);
                if (timeUp) return 0;
                if (score < best) best = score;
                if (score < beta) beta = score;
                if (alpha >= beta) break;
            }
            return best;
        }
    }

    private List<MoveEntry> GetBuffer(int ply)
    {
        while (moveBuffers.Count <= ply) moveBuffers.Add(new List<MoveEntry>());
        return moveBuffers[ply];
    }

    private List<PieceInstance> GetPieceBuffer(int ply)
    {
        while (pieceBuffers.Count <= ply) pieceBuffers.Add(new List<PieceInstance>());
        return pieceBuffers[ply];
    }

    // ================================================================
    // 候補手生成（優先度順）
    // ================================================================

    /// <summary>
    /// 手として動かせる駒か（自動移動・移動不可の駒は除く）。
    /// isManualControllable は「プレイヤーが操作できるか」なので、敵専用駒の判定には使わない
    /// </summary>
    private static bool IsControllable(PieceInstance p)
    {
        if (p.data.pieceType == PieceType.C3) return false;
        if (p.team == Team.Player && !p.data.isManualControllable) return false;
        if (p.data.isImmovable || p.data.isAutoMove) return false;
        if (p.isPromoted && p.data.isImmovableWhenPromoted) return false;
        return true;
    }

    private void GenerateOrderedMoves(Team team, List<MoveEntry> entries, List<PieceInstance> pieces)
    {
        entries.Clear();
        BoardManager bm = BoardManager.Instance;
        Team opponent = team == Team.Enemy ? Team.Player : Team.Enemy;
        PieceInstance targetC3 = bm.FindC3(opponent);
        PieceInstance ownC3 = bm.FindC3(team);
        int size = bm.CurrentBoardSize;

        bm.GetTeamPieces(team, pieces);

        foreach (PieceInstance piece in pieces)
        {
            if (!IsControllable(piece)) continue;

            MoveValidator.GetValidMoves(piece, moveScratch);
            for (int m = 0; m < moveScratch.Count; m++)
            {
                MoveValidator.MoveResult move = moveScratch[m];
                int priority;

                if (move.isAttack)
                {
                    PieceInstance target = bm.GetPieceAt(move.position);
                    if (target == null) continue;
                    // 効果のない攻撃は読まない
                    int damage = CombatResolver.CalcDamage(piece, target);
                    if (damage <= 0) continue;

                    bool kill = damage >= target.currentHP;
                    if (target.data.pieceType == PieceType.C3)
                        priority = kill ? 50000 : 10000 + damage * 200;
                    else if (kill)
                        priority = 5000 + PieceValue(target) - PieceValue(piece) / 4;
                    else
                        priority = 1000 + damage * 40;
                }
                else
                {
                    priority = 0;
                    if (targetC3 != null)
                    {
                        int curDist = Chebyshev(piece.boardPosition, targetC3.boardPosition);
                        int newDist = Chebyshev(move.position, targetC3.boardPosition);
                        priority += (curDist - newDist) * 40;
                    }
                    if (ownC3 != null && Chebyshev(move.position, ownC3.boardPosition) <= 1)
                        priority += 120;
                    // 前進
                    priority += (team == Team.Enemy ? (size - 1 - move.position.y) : move.position.y) * 2;
                    // 成る手（成ると死ぬ駒は避ける）
                    if (!piece.isPromoted && piece.data.canPromote
                        && (piece.CanPromoteAt(piece.boardPosition.y, size) || piece.CanPromoteAt(move.position.y, size)))
                        priority += piece.data.diesOnPromotion ? -3000 : 300;
                }

                entries.Add(new MoveEntry { piece = piece, move = move, priority = priority });
            }
        }

        entries.Sort((a, b) => b.priority.CompareTo(a.priority));
    }

    // ================================================================
    // 手のシミュレーション（探索用: 盤面を一時的に変更し、あとで戻す）
    // ================================================================
    private struct Saved
    {
        public PieceInstance piece;
        public int hp;
        public bool alive;
        public bool promoted;
        public Vector2Int pos;
    }

    private readonly List<Saved> undoLog = new List<Saved>();
    private readonly List<PieceInstance> restoreScratch = new List<PieceInstance>();

    private void Save(PieceInstance p)
    {
        undoLog.Add(new Saved { piece = p, hp = p.currentHP, alive = p.isAlive, promoted = p.isPromoted, pos = p.boardPosition });
    }

    /// <summary>手を盤面に適用する。戻すときは返り値を Restore に渡す</summary>
    private int Simulate(PieceInstance piece, MoveValidator.MoveResult move)
    {
        BoardManager bm = BoardManager.Instance;
        int mark = undoLog.Count;
        Save(piece);

        if (move.isAttack)
        {
            PieceInstance target = bm.GetPieceAt(move.position);
            if (target != null && target.team != piece.team)
            {
                Save(target);
                target.currentHP -= CombatResolver.CalcDamage(piece, target);
                if (target.currentHP > 0) return mark;      // 倒せなければその場に留まる
                SimKill(target, 0);
                if (!piece.isAlive) return mark;            // 髑髏の爆発で倒れた
            }
        }

        if (!bm.IsEmpty(move.position)) return mark;
        int fromRow = piece.boardPosition.y;
        bm.RemovePieceFromBoard(piece.boardPosition);
        bm.PlacePieceOnBoard(piece, move.position);

        // SN: 移動でHP-1
        if (piece.data.losesHPOnMove)
        {
            piece.currentHP--;
            if (piece.currentHP <= 0) { SimKill(piece, 0); return mark; }
        }

        // 成り（強制。敵陣に入る・敵陣から出る手で成る。成ると死ぬ駒はここで退場）
        int size = bm.CurrentBoardSize;
        if (!piece.isPromoted && piece.data.canPromote
            && (piece.CanPromoteAt(fromRow, size) || piece.CanPromoteAt(move.position.y, size)))
        {
            piece.isPromoted = true;
            int hpDiff = piece.data.promotedHP - piece.data.baseHP;
            if (hpDiff > 0) piece.currentHP += hpDiff;
            if (piece.data.diesOnPromotion) SimKill(piece, 0);
        }
        return mark;
    }

    /// <summary>探索中の撃破。髑髏なら周囲（C3以外）に貫通1ダメージ</summary>
    private void SimKill(PieceInstance victim, int chain)
    {
        BoardManager bm = BoardManager.Instance;
        if (bm.GetPieceAt(victim.boardPosition) == victim)
            bm.RemovePieceFromBoard(victim.boardPosition);
        victim.isAlive = false;

        if (victim.data.pieceType != PieceType.Dokuro || chain > 4) return;
        Vector2Int c = victim.boardPosition;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                PieceInstance n = bm.GetPieceAt(new Vector2Int(c.x + dx, c.y + dy));
                if (n == null || !n.isAlive || n.data.pieceType == PieceType.C3) continue;
                Save(n);
                n.currentHP -= 1;
                if (n.currentHP <= 0) SimKill(n, chain + 1);
            }
        }
    }

    private void Restore(int mark)
    {
        BoardManager bm = BoardManager.Instance;
        restoreScratch.Clear();

        // 変更のあった駒を盤から外し、最初に保存した状態へ戻す
        for (int i = undoLog.Count - 1; i >= mark; i--)
        {
            Saved s = undoLog[i];
            PieceInstance p = s.piece;
            if (bm.IsInBounds(p.boardPosition) && bm.GetPieceAt(p.boardPosition) == p)
                bm.RemovePieceFromBoard(p.boardPosition);
            p.currentHP = s.hp;
            p.isAlive = s.alive;
            p.isPromoted = s.promoted;
            p.boardPosition = s.pos;
            if (!restoreScratch.Contains(p)) restoreScratch.Add(p);
        }
        undoLog.RemoveRange(mark, undoLog.Count - mark);

        // 生きていた駒を元の位置に置き直す
        foreach (var p in restoreScratch)
            if (p.isAlive) bm.PlacePieceOnBoard(p, p.boardPosition);
    }

    // ================================================================
    // 局面評価（正の値 = AI有利）
    // ================================================================
    private readonly List<PieceInstance> evalEnemy = new List<PieceInstance>();
    private readonly List<PieceInstance> evalPlayer = new List<PieceInstance>();
    // 各マスに届く駒の数と最大攻撃力（陣営別）
    private int[] reachCountE, reachMaxAtkE, reachSumAtkE;
    private int[] reachCountP, reachMaxAtkP, reachSumAtkP;

    private int PieceValue(PieceInstance p)
    {
        int hp = Mathf.Min(p.currentHP, 20);
        int v = p.ATK * 30 + p.DEF * 22 + hp * 18 + 20;
        if (p.isPromoted) v += 30;
        return v;
    }

    private int Evaluate(Team toMove)
    {
        BoardManager bm = BoardManager.Instance;
        PieceInstance pC3 = bm.FindC3(Team.Player);
        PieceInstance eC3 = bm.FindC3(Team.Enemy);
        if (pC3 == null) return WinScore;
        if (eC3 == null) return -WinScore;

        int size = bm.CurrentBoardSize;
        bm.GetTeamPieces(Team.Enemy, evalEnemy);
        bm.GetTeamPieces(Team.Player, evalPlayer);
        BuildReach(evalEnemy, size, ref reachCountE, ref reachMaxAtkE, ref reachSumAtkE);
        BuildReach(evalPlayer, size, ref reachCountP, ref reachMaxAtkP, ref reachSumAtkP);

        int score = 0;

        // 1. 戦力
        foreach (var p in evalEnemy) if (p.data.pieceType != PieceType.C3) score += PieceValue(p);
        foreach (var p in evalPlayer) if (p.data.pieceType != PieceType.C3) score -= PieceValue(p);

        // 2. C3の体力
        score += eC3.currentHP * 300 - pC3.currentHP * 300;
        if (pC3.currentHP <= 2) score += 1500;
        if (eC3.currentHP <= 2) score -= 1500;

        // 3. C3の護衛
        score += CountAdjacent(eC3.boardPosition, Team.Enemy) * 150;
        score -= CountAdjacent(pC3.boardPosition, Team.Player) * 150;

        // 4. C3への脅威（届く駒の数と合計ダメージ）
        int pIdx = pC3.boardPosition.x + pC3.boardPosition.y * size;
        int eIdx = eC3.boardPosition.x + eC3.boardPosition.y * size;
        int threatsToP = reachCountE[pIdx];
        int threatsToE = reachCountP[eIdx];
        int dmgToP = Mathf.Max(0, reachSumAtkE[pIdx] - threatsToP * pC3.DEF);
        int dmgToE = Mathf.Max(0, reachSumAtkP[eIdx] - threatsToE * eC3.DEF);
        score += threatsToP * 450 - threatsToE * 450;
        if (dmgToP >= pC3.currentHP && threatsToP > 0) score += toMove == Team.Enemy ? 20000 : 3000;
        if (dmgToE >= eC3.currentHP && threatsToE > 0) score -= toMove == Team.Player ? 20000 : 3000;

        // 5. 取られそうな駒（手番側は取れる、相手側は逃げる余地がある）
        int bestEnemyCapture = 0, bestPlayerCapture = 0;
        int enemyHanging = 0, playerHanging = 0;
        foreach (var p in evalPlayer)
        {
            if (p.data.pieceType == PieceType.C3) continue;
            int idx = p.boardPosition.x + p.boardPosition.y * size;
            if (reachCountE[idx] > 0 && reachMaxAtkE[idx] - p.DEF >= p.currentHP)
            {
                int v = PieceValue(p);
                playerHanging += v;
                if (v > bestEnemyCapture) bestEnemyCapture = v;
            }
        }
        foreach (var p in evalEnemy)
        {
            if (p.data.pieceType == PieceType.C3) continue;
            int idx = p.boardPosition.x + p.boardPosition.y * size;
            if (reachCountP[idx] > 0 && reachMaxAtkP[idx] - p.DEF >= p.currentHP)
            {
                int v = PieceValue(p);
                enemyHanging += v;
                if (v > bestPlayerCapture) bestPlayerCapture = v;
            }
        }
        if (toMove == Team.Enemy)
            score += bestEnemyCapture * 8 / 10 - enemyHanging * 3 / 10;
        else
            score -= bestPlayerCapture * 8 / 10 - playerHanging * 3 / 10;

        // 6. 前進と敵C3への接近（動かせる駒のみ）
        foreach (var p in evalEnemy)
        {
            if (!IsControllable(p)) continue;
            score += (10 - Chebyshev(p.boardPosition, pC3.boardPosition)) * 4;
            score += (size - 1 - p.boardPosition.y) * 3;
        }
        foreach (var p in evalPlayer)
        {
            if (!IsControllable(p)) continue;
            score -= (10 - Chebyshev(p.boardPosition, eC3.boardPosition)) * 4;
            score -= p.boardPosition.y * 3;
        }

        return score;
    }

    /// <summary>陣営の駒が次の手で攻撃できるマスを集計する（自動移動の門人も含む）</summary>
    private static void BuildReach(List<PieceInstance> pieces, int size, ref int[] count, ref int[] maxAtk, ref int[] sumAtk)
    {
        int n = size * size;
        if (count == null || count.Length != n)
        {
            count = new int[n];
            maxAtk = new int[n];
            sumAtk = new int[n];
        }
        else
        {
            System.Array.Clear(count, 0, n);
            System.Array.Clear(maxAtk, 0, n);
            System.Array.Clear(sumAtk, 0, n);
        }

        BoardManager bm = BoardManager.Instance;
        foreach (var p in pieces)
        {
            if (p.data.pieceType == PieceType.C3 || p.data.isImmovable) continue;
            if (p.isPromoted && p.data.isImmovableWhenPromoted) continue;
            int atk = p.ATK;
            if (atk <= 0) continue;

            MoveDirection[] dirs = p.GetMoveDirections();
            for (int i = 0; i < dirs.Length; i++)
            {
                MoveDirection dir = dirs[i];
                for (int d = 1; d <= dir.maxDistance; d++)
                {
                    int x = p.boardPosition.x + dir.direction.x * d;
                    int y = p.boardPosition.y + dir.direction.y * d;
                    if (x < 0 || y < 0 || x >= size || y >= size) break;
                    int idx = x + y * size;
                    count[idx]++;
                    sumAtk[idx] += atk;
                    if (atk > maxAtk[idx]) maxAtk[idx] = atk;
                    // 飛び越えられない方向は駒に当たったら止まる（MoveValidatorと同じ規則）
                    if (!dir.canJump && bm.GetPieceAt(new Vector2Int(x, y)) != null) break;
                }
            }
        }
    }

    private static int CountAdjacent(Vector2Int pos, Team team)
    {
        BoardManager bm = BoardManager.Instance;
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                PieceInstance piece = bm.GetPieceAt(new Vector2Int(pos.x + dx, pos.y + dy));
                if (piece != null && piece.team == team) count++;
            }
        }
        return count;
    }

    private static int Chebyshev(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}
