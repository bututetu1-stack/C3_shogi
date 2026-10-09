using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AbilitySystem : MonoBehaviour
{
    public static AbilitySystem Instance { get; private set; }

    private static int nextGroupId = 0;

    // 黄泉の召喚回数追跡
    private Dictionary<PieceInstance, int> yomigaeruSpawnCount = new Dictionary<PieceInstance, int>();

    // 髑髏の連鎖爆発防止
    private HashSet<Vector2Int> explodingPositions = new HashSet<Vector2Int>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ============================================================
    // ターン開始時能力 (門人自動移動 + ヲツ中華生成)
    // ============================================================
    public IEnumerator ExecuteTurnStartAbilities(Team team)
    {
        BoardManager bm = BoardManager.Instance;
        List<PieceInstance> pieces = bm.GetTeamPieces(team);

        // ヲツの中華生成
        var wotsuList = new List<PieceInstance>();
        foreach (var p in pieces)
        {
            if (p.data.pieceType == PieceType.Wotsu && p.isAlive)
                wotsuList.Add(p);
        }
        foreach (var wotsu in wotsuList)
        {
            int count = wotsu.isPromoted ? 3 : 1;
            yield return SpawnChuka(wotsu.team, count);
        }

        // 門人の自動移動
        pieces = bm.GetTeamPieces(team);
        var moninList = new List<PieceInstance>();
        foreach (var p in pieces)
        {
            if (p.data.pieceType == PieceType.Monin && p.isAlive)
                moninList.Add(p);
        }
        foreach (var monin in moninList)
        {
            if (!monin.isAlive) continue;

            // 成り後は50%で絶起（行動スキップ）
            if (monin.isPromoted && Random.value < 0.5f)
            {
                Debug.Log(monin.DisplayName + " は絶起した！");
                continue;
            }

            yield return ExecuteAutoMove(monin);
        }

        // なこの能力
        pieces = bm.GetTeamPieces(team);
        var nakoList = new List<PieceInstance>();
        foreach (var p in pieces)
        {
            if (p.data.pieceType == PieceType.Nako && p.isAlive)
                nakoList.Add(p);
        }
        foreach (var nako in nakoList)
        {
            if (!nako.isAlive) continue;
            yield return ExecuteNakoAbility(nako);
        }

        // 黄泉の歩召喚（敵ターン開始時）
        if (team == Team.Enemy)
        {
            pieces = bm.GetTeamPieces(team);
            var yomiList = new List<PieceInstance>();
            foreach (var p in pieces)
            {
                if (p.data.pieceType == PieceType.Yomigaeru && p.isAlive)
                    yomiList.Add(p);
            }
            foreach (var yomi in yomiList)
            {
                if (!yomi.isAlive) continue;
                int used = 0;
                if (yomigaeruSpawnCount.ContainsKey(yomi))
                    used = yomigaeruSpawnCount[yomi];
                if (used >= 2) continue; // max 2 spawns per Yomigaeru

                int boardSize = bm.CurrentBoardSize;
                int halfBoard = boardSize / 2;
                var emptyPositions = new List<Vector2Int>();
                for (int x = 0; x < boardSize; x++)
                {
                    for (int y = halfBoard; y < boardSize; y++)
                    {
                        Vector2Int pos = new Vector2Int(x, y);
                        if (bm.IsEmpty(pos)) emptyPositions.Add(pos);
                    }
                }
                if (emptyPositions.Count > 0)
                {
                    Vector2Int spawnPos = emptyPositions[Random.Range(0, emptyPositions.Count)];
                    PieceData pawnData = bm.GetPieceDataByType(PieceType.Pawn);
                    if (pawnData != null)
                    {
                        bm.SpawnPiece(pawnData, Team.Enemy, spawnPos);
                        if (yomigaeruSpawnCount.ContainsKey(yomi))
                            yomigaeruSpawnCount[yomi] = used + 1;
                        else
                            yomigaeruSpawnCount[yomi] = 1;
                        if (BattleLogUI.Instance != null)
                            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("黄泉", Team.Enemy) + " が歩兵を召喚！");
                        yield return new WaitForSeconds(0.3f);
                    }
                }
            }

            // 魔王の雷撃（敵ターン開始時）
            pieces = bm.GetTeamPieces(Team.Enemy);
            var maouList = new List<PieceInstance>();
            foreach (var p in pieces)
            {
                if (p.data.pieceType == PieceType.Maou && p.isAlive)
                    maouList.Add(p);
            }
            foreach (var maou in maouList)
            {
                if (!maou.isAlive) continue;
                List<PieceInstance> playerPieces = bm.GetTeamPieces(Team.Player);
                var targets = new List<PieceInstance>();
                foreach (var pp in playerPieces)
                {
                    if (pp.isAlive && pp.data.pieceType != PieceType.C3)
                        targets.Add(pp);
                }
                if (targets.Count > 0)
                {
                    PieceInstance victim = targets[Random.Range(0, targets.Count)];
                    if (BattleLogUI.Instance != null)
                        BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("魔王", Team.Enemy) + " の雷撃！" + BattleLogUI.ColorName(victim.DisplayName, victim.team) + " に貫通1ダメージ");
                    if (BattleEffects.Instance != null)
                        BattleEffects.Instance.PlayHitEffect(victim.boardPosition);
                    DealPiercingDamage(victim, 1);
                    yield return new WaitForSeconds(0.4f);
                }
            }
        }
    }

    // ============================================================
    // ターン終了時能力 (中華回復消滅 + 深海攻撃 + 艦娘攻撃)
    // ============================================================
    public IEnumerator ExecuteTurnEndAbilities(Team team)
    {
        BoardManager bm = BoardManager.Instance;

        // 中華の回復と消滅
        List<PieceInstance> pieces = bm.GetTeamPieces(team);
        var chukaList = new List<PieceInstance>();
        foreach (var p in pieces)
        {
            if (p.data.pieceType == PieceType.Chuka && p.isAlive)
                chukaList.Add(p);
        }
        foreach (var chuka in chukaList)
        {
            HealAdjacentAllies(chuka);
            Vector2Int pos = chuka.boardPosition;
            if (BattleEffects.Instance != null)
                BattleEffects.Instance.PlayDefeatEffect(pos);
            bm.RemovePieceController(pos);
            bm.RemovePiece(pos);
        }

        // 閻魔の回復（消滅しない版、敵ターン終了時）
        if (team == Team.Enemy)
        {
            pieces = bm.GetTeamPieces(team);
            var enmashiList = new List<PieceInstance>();
            foreach (var p in pieces)
            {
                if (p.data.pieceType == PieceType.Enmashi && p.isAlive)
                    enmashiList.Add(p);
            }
            foreach (var enmashi in enmashiList)
            {
                if (!enmashi.isAlive) continue;
                HealAdjacentAllies(enmashi);
            }
        }

        // 軍将のATKバフ（敵ターン終了時）
        if (team == Team.Enemy)
        {
            pieces = bm.GetTeamPieces(team);
            var gunList = new List<PieceInstance>();
            foreach (var p in pieces)
            {
                if (p.data.pieceType == PieceType.Gundaishou && p.isAlive)
                    gunList.Add(p);
            }
            foreach (var gun in gunList)
            {
                if (!gun.isAlive) continue;
                Vector2Int center = gun.boardPosition;
                bool buffed = false;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        Vector2Int pos = new Vector2Int(center.x + dx, center.y + dy);
                        if (!bm.IsInBounds(pos)) continue;
                        PieceInstance target = bm.GetPieceAt(pos);
                        if (target != null && target.team == gun.team && target != gun
                            && target.data.pieceType != PieceType.C3)
                        {
                            target.bonusATK += 1;
                            PieceController pc = bm.GetPieceController(pos);
                            if (pc != null) pc.GetRenderer().UpdateAllStats();
                            buffed = true;
                        }
                    }
                }
                if (buffed && BattleLogUI.Instance != null)
                    BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("軍将", Team.Enemy) + " が味方を鼓舞！ATK+1");
            }
        }

        // 深海の攻撃 (敵ターン終了時)
        if (team == Team.Enemy)
        {
            yield return ExecuteShinkaiAbilities();
        }

        // 艦娘の攻撃 (プレイヤーターン終了時)
        if (team == Team.Player)
        {
            yield return ExecuteKanmusuAbilities();
        }

        // 僕バフ処理
        yield return ProcessBokuBuffs(team);
    }

    // ============================================================
    // 李白の裏返し能力（手動移動後に発動）
    // ============================================================
    public void ExecuteRihakuAbility(PieceInstance rihaku)
    {
        if (rihaku == null || rihaku.data.pieceType != PieceType.Rihaku) return;

        int radius = rihaku.isPromoted ? 2 : 1;
        int flipCount = rihaku.isPromoted ? 3 : 1;

        BoardManager bm = BoardManager.Instance;
        Vector2Int center = rihaku.boardPosition;

        // 範囲内の自分以外の駒を収集（immuneToFlip除外）
        var candidates = new List<PieceInstance>();
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int pos = new Vector2Int(center.x + dx, center.y + dy);
                if (!bm.IsInBounds(pos)) continue;
                PieceInstance target = bm.GetPieceAt(pos);
                if (target != null && target != rihaku
                    && target.data.pieceType != PieceType.C3
                    && !target.data.immuneToFlip)
                    candidates.Add(target);
            }
        }

        // ランダムにflipCount枚裏返す
        int flipped = 0;
        while (flipped < flipCount && candidates.Count > 0)
        {
            int idx = Random.Range(0, candidates.Count);
            PieceInstance target = candidates[idx];
            candidates.RemoveAt(idx);

            if (target.data.canPromote)
            {
                if (target.isPromoted)
                {
                    target.isPromoted = false;
                    int hpDiff = target.data.promotedHP - target.data.baseHP;
                    if (hpDiff > 0)
                    {
                        target.currentHP -= hpDiff;
                        if (target.currentHP < 1) target.currentHP = 1;
                    }
                }
                else
                {
                    target.Promote();
                }

                PieceController pc = bm.GetPieceController(target.boardPosition);
                if (pc != null) pc.GetRenderer().UpdateAllStats();

                flipped++;
                Debug.Log("李白が " + target.DisplayName + " を裏返した！");
                if (BattleLogUI.Instance != null)
                    BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("李白", rihaku.team) + "が " + BattleLogUI.ColorName(target.DisplayName, target.team) + " を裏返した");
            }
        }
    }

    // ============================================================
    // 提督の成り時特殊処理（ワープ + 深海3体 + 艦娘2体召喚）
    // ============================================================
    public void ExecuteTeitokuPromotion(PieceInstance teitoku)
    {
        BoardManager bm = BoardManager.Instance;
        int groupId = nextGroupId++;
        teitoku.linkedGroupId = groupId;

        int size = bm.CurrentBoardSize;
        int halfBoard = size / 2;
        int promoteRows = BoardCell.GetPromoteRows(size);

        // 提督をプレイヤー側の成りゾーン（自陣下段）にワープ
        Vector2Int oldPos = teitoku.boardPosition;
        Vector2Int? newPos = FindEmptyInRange(0, promoteRows);
        if (newPos.HasValue)
        {
            PieceController pc = bm.GetPieceController(oldPos);
            bm.MovePiece(oldPos, newPos.Value);
            bm.UpdatePieceControllerPosition(oldPos, newPos.Value);
            if (pc != null) pc.MoveTo(newPos.Value);
        }

        // 提督の最終位置を取得（深海との間隔チェック用）
        Vector2Int teitokuPos = teitoku.boardPosition;

        // 深海3体を敵として敵陣～中央（上半分）に生成（提督と最低2マス間隔）
        PieceData shinkaiData = bm.GetPieceDataByType(PieceType.Shinkai);
        if (shinkaiData != null)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2Int? spawnPos = FindEmptyWithMinDistance(halfBoard, size, teitokuPos, 2);
                if (spawnPos.HasValue)
                {
                    PieceController pc = bm.SpawnPiece(shinkaiData, Team.Enemy, spawnPos.Value);
                    if (pc != null)
                    {
                        PieceInstance spawned = pc.GetPiece();
                        if (spawned != null) spawned.linkedGroupId = groupId;
                    }
                }
            }
        }

        // 艦娘2体をプレイヤーとして自軍エリア（下半分）に生成
        PieceData kanmusuData = bm.GetPieceDataByType(PieceType.Kanmusu);
        if (kanmusuData != null)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2Int? spawnPos = FindEmptyInRange(0, halfBoard + 1);
                if (spawnPos.HasValue)
                {
                    PieceController pc = bm.SpawnPiece(kanmusuData, Team.Player, spawnPos.Value);
                    if (pc != null)
                    {
                        PieceInstance spawned = pc.GetPiece();
                        if (spawned != null) spawned.linkedGroupId = groupId;
                    }
                }
            }
        }

        Debug.Log("物鉄が提督に成った！ワープ＋深海3体＋艦娘2体召喚！");
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("物鉄", teitoku.team) + "が提督に！深海3+艦娘2召喚");
    }

    // ============================================================
    // リンク消滅ロジック
    // ============================================================
    public void CheckLinkedDeaths(int groupId)
    {
        if (groupId < 0) return;

        BoardManager bm = BoardManager.Instance;
        var allAlive = new List<PieceInstance>();
        allAlive.AddRange(bm.GetTeamPieces(Team.Player));
        allAlive.AddRange(bm.GetTeamPieces(Team.Enemy));

        bool teitokuAlive = false;
        PieceInstance teitoku = null;
        bool anyShinkaiAlive = false;
        var kanmusuList = new List<PieceInstance>();

        foreach (var p in allAlive)
        {
            if (p.linkedGroupId != groupId || !p.isAlive) continue;

            if (p.data.pieceType == PieceType.Monotetsu && p.isPromoted)
            {
                teitokuAlive = true;
                teitoku = p;
            }
            else if (p.data.pieceType == PieceType.Shinkai)
            {
                anyShinkaiAlive = true;
            }
            else if (p.data.pieceType == PieceType.Kanmusu)
            {
                kanmusuList.Add(p);
            }
        }

        bool removedTeitoku = false;

        // 全深海撃破 → 提督撤退
        if (!anyShinkaiAlive && teitokuAlive && teitoku != null)
        {
            Vector2Int pos = teitoku.boardPosition;
            if (BattleEffects.Instance != null)
                BattleEffects.Instance.PlayDefeatEffect(pos);
            bm.RemovePiece(pos);
            bm.RemovePieceController(pos);
            removedTeitoku = true;
            Debug.Log("全深海撃破 → 提督撤退！");
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog("全" + BattleLogUI.ColorName("深海", Team.Enemy) + "撃破 → " + BattleLogUI.ColorName("提督", Team.Player) + "撤退！");
        }

        // 提督撃破 → 艦娘全滅（提督が元から死亡 or 上で撤退した場合）
        if ((!teitokuAlive || removedTeitoku) && kanmusuList.Count > 0)
        {
            foreach (var kanmusu in kanmusuList)
            {
                if (!kanmusu.isAlive) continue;
                Vector2Int pos = kanmusu.boardPosition;
                if (BattleEffects.Instance != null)
                    BattleEffects.Instance.PlayDefeatEffect(pos);
                bm.RemovePiece(pos);
                bm.RemovePieceController(pos);
            }
            Debug.Log("提督撃破 → 艦娘全滅！");
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("提督", Team.Player) + "撃破 → " + BattleLogUI.ColorName("艦娘", Team.Player) + "全滅！");
        }
    }

    // ============================================================
    // 深海のターン終了時攻撃
    // ============================================================
    private IEnumerator ExecuteShinkaiAbilities()
    {
        BoardManager bm = BoardManager.Instance;
        var shinkaiList = new List<PieceInstance>();
        var pieces = bm.GetTeamPieces(Team.Enemy);
        foreach (var p in pieces)
        {
            if (p.data.pieceType == PieceType.Shinkai && p.isAlive)
                shinkaiList.Add(p);
        }
        foreach (var shinkai in shinkaiList)
        {
            if (!shinkai.isAlive) continue;
            yield return ExecuteShinkaiAttack(shinkai);
        }
    }

    private IEnumerator ExecuteShinkaiAttack(PieceInstance shinkai)
    {
        BoardManager bm = BoardManager.Instance;
        Vector2Int center = shinkai.boardPosition;

        // 周囲1マスのプレイヤー駒を収集
        var targets = new List<PieceInstance>();
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int pos = new Vector2Int(center.x + dx, center.y + dy);
                if (!bm.IsInBounds(pos)) continue;
                PieceInstance target = bm.GetPieceAt(pos);
                if (target != null && target.team == Team.Player && target.isAlive)
                    targets.Add(target);
            }
        }

        if (targets.Count == 0) yield break;

        // ランダム1枚に通常2ダメージ
        PieceInstance victim = targets[Random.Range(0, targets.Count)];
        DealNormalDamage(victim, 2);

        Debug.Log("深海が " + victim.DisplayName + " に攻撃！");
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("深海", shinkai.team) + "が " + BattleLogUI.ColorName(victim.DisplayName, victim.team) + " に攻撃");
        yield return new WaitForSeconds(0.3f);
    }

    // ============================================================
    // 艦娘のターン終了時攻撃 (空爆/雷撃/砲撃)
    // ============================================================
    private IEnumerator ExecuteKanmusuAbilities()
    {
        BoardManager bm = BoardManager.Instance;
        var kanmusuList = new List<PieceInstance>();
        var pieces = bm.GetTeamPieces(Team.Player);
        foreach (var p in pieces)
        {
            if (p.data.pieceType == PieceType.Kanmusu && p.isAlive)
                kanmusuList.Add(p);
        }
        foreach (var kanmusu in kanmusuList)
        {
            if (!kanmusu.isAlive) continue;
            yield return ExecuteKanmusuAttack(kanmusu);
        }
    }

    private IEnumerator ExecuteKanmusuAttack(PieceInstance kanmusu)
    {
        if (!kanmusu.isAlive) yield break;

        BoardManager bm = BoardManager.Instance;

        // 盤上のランダムな深海を選択（毎回再スキャン）
        var shinkaiList = new List<PieceInstance>();
        var enemyPieces = bm.GetTeamPieces(Team.Enemy);
        foreach (var p in enemyPieces)
        {
            if (p.data.pieceType == PieceType.Shinkai && p.isAlive)
                shinkaiList.Add(p);
        }

        if (shinkaiList.Count == 0) yield break;

        PieceInstance targetShinkai = shinkaiList[Random.Range(0, shinkaiList.Count)];

        // 空爆/雷撃/砲撃からランダム選択
        int attackType = Random.Range(0, 3);
        switch (attackType)
        {
            case 0:
                yield return ExecuteAirRaid(kanmusu, targetShinkai);
                break;
            case 1:
                yield return ExecuteTorpedo(kanmusu, targetShinkai);
                break;
            case 2:
                yield return ExecuteBombardment(kanmusu, targetShinkai);
                break;
        }
    }

    // --- 空爆 ---
    private IEnumerator ExecuteAirRaid(PieceInstance kanmusu, PieceInstance shinkai)
    {
        if (!shinkai.isAlive) yield break;

        Vector2Int targetPos = shinkai.boardPosition;

        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayAirRaidEffect(targetPos);

        yield return new WaitForSeconds(0.8f);

        // 深海に防御貫通1ダメージ
        if (shinkai.isAlive)
            DealPiercingDamage(shinkai, 1);

        // 深海の周囲2マス以内のランダムな敵駒3枚に1ダメージ
        var nearby = GetEnemiesInRange(targetPos, 2, Team.Enemy, shinkai);
        ShuffleList(nearby);
        int splashCount = Mathf.Min(3, nearby.Count);
        for (int i = 0; i < splashCount; i++)
        {
            if (nearby[i].isAlive)
                DealNormalDamage(nearby[i], 1);
        }

        Debug.Log("艦娘が空爆を実行！");
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("艦娘", kanmusu.team) + "が空爆！");
        yield return new WaitForSeconds(0.2f);
    }

    // --- 雷撃 ---
    private IEnumerator ExecuteTorpedo(PieceInstance kanmusu, PieceInstance shinkai)
    {
        if (!kanmusu.isAlive || !shinkai.isAlive) yield break;

        BoardManager bm = BoardManager.Instance;
        Vector2Int from = kanmusu.boardPosition;
        Vector2Int to = shinkai.boardPosition;

        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayTorpedoEffect(from, to);

        yield return new WaitForSeconds(1.0f);

        // Bresenham直線上のマスを取得（始点・終点含まない）
        List<Vector2Int> line = GetBresenhamLine(from, to);

        // 直線上の敵駒を収集
        var lineEnemies = new List<PieceInstance>();
        foreach (var pos in line)
        {
            PieceInstance piece = bm.GetPieceAt(pos);
            if (piece != null && piece.team == Team.Enemy && piece.isAlive)
                lineEnemies.Add(piece);
        }

        if (lineEnemies.Count > 0)
        {
            // 直線上の最も近い敵駒1体に3ダメージ
            PieceInstance nearest = lineEnemies[0];
            if (nearest.isAlive)
                DealNormalDamage(nearest, 3);
            Debug.Log("雷撃！" + nearest.DisplayName + " に3ダメージ！");
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog("雷撃！" + BattleLogUI.ColorName(nearest.DisplayName, nearest.team) + " に3ダメージ");
        }
        else
        {
            // 直線上に敵駒がいない: 深海に防御貫通2ダメージ
            if (shinkai.isAlive)
                DealPiercingDamage(shinkai, 2);
            Debug.Log("雷撃！深海に防御貫通2ダメージ！");
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog("雷撃！深海に防御貫通2ダメージ");
        }

        yield return new WaitForSeconds(0.2f);
    }

    // --- 砲撃 ---
    private IEnumerator ExecuteBombardment(PieceInstance kanmusu, PieceInstance shinkai)
    {
        if (!shinkai.isAlive) yield break;

        Vector2Int targetPos = shinkai.boardPosition;

        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayBombardmentEffect(targetPos);

        yield return new WaitForSeconds(1.0f);

        // 深海に防御貫通4ダメージ
        if (shinkai.isAlive)
            DealPiercingDamage(shinkai, 4);

        // 深海の周囲1マス以内のランダムな敵駒2枚に2ダメージ
        var nearby = GetEnemiesInRange(targetPos, 1, Team.Enemy, shinkai);
        ShuffleList(nearby);
        int splashCount = Mathf.Min(2, nearby.Count);
        for (int i = 0; i < splashCount; i++)
        {
            if (nearby[i].isAlive)
                DealNormalDamage(nearby[i], 2);
        }

        Debug.Log("艦娘が砲撃を実行！");
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("艦娘", kanmusu.team) + "が砲撃！");
        yield return new WaitForSeconds(0.2f);
    }

    // ============================================================
    // なこの能力（ドパ生成→突撃→パス上敵ダメージ）
    // ============================================================
    // 8方向ベクトル
    private static readonly Vector2Int[] EightDirections = new Vector2Int[]
    {
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    private IEnumerator ExecuteNakoAbility(PieceInstance nako)
    {
        BoardManager bm = BoardManager.Instance;
        int runs = nako.isPromoted ? 2 : 1;

        for (int run = 0; run < runs; run++)
        {
            if (!nako.isAlive) yield break;

            Vector2Int nakoPos = nako.boardPosition;

            // 8方向から距離2以上の空きマスを方向別に収集
            var candidates = new List<Vector2Int>();
            foreach (var dir in EightDirections)
            {
                for (int dist = 2; dist < bm.CurrentBoardSize; dist++)
                {
                    Vector2Int pos = nakoPos + dir * dist;
                    if (!bm.IsInBounds(pos)) break;
                    if (bm.IsEmpty(pos))
                        candidates.Add(pos);
                }
            }

            if (candidates.Count == 0) yield break;

            Vector2Int dopaPos = candidates[Random.Range(0, candidates.Count)];

            // ドパ生成（敵チームとして出現）
            PieceData dopaData = bm.GetPieceDataByType(PieceType.Dopa);
            if (dopaData == null) yield break;

            Team dopaTeam = (nako.team == Team.Player) ? Team.Enemy : Team.Player;
            PieceController dopaPC = bm.SpawnPiece(dopaData, dopaTeam, dopaPos);

            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("なこ", nako.team) + "がドパを召喚！");

            yield return new WaitForSeconds(0.3f);

            // なこからドパへの直線パスを取得（始点・終点含まない）
            List<Vector2Int> path = GetBresenhamLine(nakoPos, dopaPos);

            // なこをスライド移動（ドパはまだ残す）
            Vector2Int nakoFrom = nakoPos;
            PieceController nakoPC = bm.GetPieceController(nakoFrom);

            // ボード上の移動はアニメ後に行うため、まずアニメだけ実行
            if (nakoPC != null)
            {
                float slideDuration = 0.4f;
                yield return nakoPC.SlideToCoroutine(dopaPos, slideDuration);
            }

            if (BattleEffects.Instance != null)
                BattleEffects.Instance.PlayMoveEffect();

            // なこ到着後にドパを撃破エフェクト付きで除去
            PieceInstance dopaInstance = bm.GetPieceAt(dopaPos);
            if (dopaInstance != null && dopaInstance.data.pieceType == PieceType.Dopa)
            {
                if (BattleEffects.Instance != null)
                    BattleEffects.Instance.PlayDefeatEffect(dopaPos);
                bm.RemovePiece(dopaPos);
                bm.RemovePieceController(dopaPos);
            }

            // ボード上でなこを移動
            bm.MovePiece(nakoFrom, dopaPos);
            bm.UpdatePieceControllerPosition(nakoFrom, dopaPos);

            // パス上の敵駒に2ダメージ（スライド到着後）
            foreach (var pos in path)
            {
                PieceInstance target = bm.GetPieceAt(pos);
                if (target != null && target.team != nako.team && target.isAlive)
                {
                    DealNormalDamage(target, 2);
                    if (BattleLogUI.Instance != null)
                        BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("なこ", nako.team) + "の突撃！" + BattleLogUI.ColorName(target.DisplayName, target.team) + " に2ダメージ");
                    yield return new WaitForSeconds(0.15f);
                }
            }

            // 成りチェック
            GameManager.Instance.CheckPromotion(nako);

            yield return new WaitForSeconds(0.3f);
        }
    }

    // ============================================================
    // ダメージヘルパー
    // ============================================================
    private void DealPiercingDamage(PieceInstance target, int damage)
    {
        if (target == null || !target.isAlive) return;
        BoardManager bm = BoardManager.Instance;

        // 挑発駒はダメージ無効（∞HP）
        if (target.data.isTauntPiece)
        {
            if (BattleEffects.Instance != null)
                BattleEffects.Instance.PlayHitEffect(target.boardPosition);
            PieceController tpc = bm.GetPieceController(target.boardPosition);
            if (tpc != null) tpc.Shake();
            return;
        }

        target.currentHP -= damage;
        PieceController pc = bm.GetPieceController(target.boardPosition);
        if (pc != null) pc.UpdateHP();

        if (target.currentHP <= 0)
        {
            if (BattleEffects.Instance != null)
                BattleEffects.Instance.PlayDefeatEffect(target.boardPosition);
            int groupId = target.linkedGroupId;
            Vector2Int pos = target.boardPosition;
            bm.RemovePiece(pos);
            bm.RemovePieceController(pos);
            CheckLinkedDeaths(groupId);
        }
        else
        {
            if (BattleEffects.Instance != null)
                BattleEffects.Instance.PlayHitEffect(target.boardPosition);
            if (pc != null) pc.Shake();
        }
    }

    private void DealNormalDamage(PieceInstance target, int baseDamage)
    {
        if (target == null || !target.isAlive) return;
        BoardManager bm = BoardManager.Instance;

        // 挑発駒はダメージ無効（∞HP）
        if (target.data.isTauntPiece)
        {
            if (BattleEffects.Instance != null)
                BattleEffects.Instance.PlayHitEffect(target.boardPosition);
            PieceController tpc = bm.GetPieceController(target.boardPosition);
            if (tpc != null) tpc.Shake();
            return;
        }

        int damage = Mathf.Max(0, baseDamage - target.DEF);
        target.currentHP -= damage;
        PieceController pc = bm.GetPieceController(target.boardPosition);
        if (pc != null) pc.UpdateHP();

        if (target.currentHP <= 0)
        {
            if (BattleEffects.Instance != null)
                BattleEffects.Instance.PlayDefeatEffect(target.boardPosition);
            int groupId = target.linkedGroupId;
            Vector2Int pos = target.boardPosition;
            bm.RemovePiece(pos);
            bm.RemovePieceController(pos);
            CheckLinkedDeaths(groupId);
        }
        else
        {
            if (BattleEffects.Instance != null)
                BattleEffects.Instance.PlayHitEffect(target.boardPosition);
            if (pc != null) pc.Shake();
        }
    }

    // ============================================================
    // Bresenhamの直線アルゴリズム（始点・終点を含まない）
    // ============================================================
    private List<Vector2Int> GetBresenhamLine(Vector2Int from, Vector2Int to)
    {
        var line = new List<Vector2Int>();
        if (from.x == to.x && from.y == to.y) return line;

        int x0 = from.x, y0 = from.y;
        int x1 = to.x, y1 = to.y;
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }

            // 終点に到達したら終了（終点は含まない）
            if (x0 == x1 && y0 == y1) break;

            line.Add(new Vector2Int(x0, y0));
        }

        return line;
    }

    // ============================================================
    // ユーティリティ
    // ============================================================
    private Vector2Int? FindEmptyInRange(int minY, int maxY)
    {
        BoardManager bm = BoardManager.Instance;
        var emptyPositions = new List<Vector2Int>();
        for (int x = 0; x < bm.CurrentBoardSize; x++)
        {
            for (int y = minY; y < maxY; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (bm.IsEmpty(pos))
                    emptyPositions.Add(pos);
            }
        }
        if (emptyPositions.Count == 0) return null;
        return emptyPositions[Random.Range(0, emptyPositions.Count)];
    }

    // 指定座標からminDist以上離れた空きマスを探す（Chebyshev距離）
    private Vector2Int? FindEmptyWithMinDistance(int minY, int maxY, Vector2Int avoidPos, int minDist)
    {
        BoardManager bm = BoardManager.Instance;
        var emptyPositions = new List<Vector2Int>();
        for (int x = 0; x < bm.CurrentBoardSize; x++)
        {
            for (int y = minY; y < maxY; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (!bm.IsEmpty(pos)) continue;
                int dist = Mathf.Max(Mathf.Abs(pos.x - avoidPos.x), Mathf.Abs(pos.y - avoidPos.y));
                if (dist >= minDist)
                    emptyPositions.Add(pos);
            }
        }
        // 距離条件を満たすマスがなければ条件なしでフォールバック
        if (emptyPositions.Count == 0)
            return FindEmptyInRange(minY, maxY);
        return emptyPositions[Random.Range(0, emptyPositions.Count)];
    }

    private List<PieceInstance> GetEnemiesInRange(Vector2Int center, int range, Team enemyTeam, PieceInstance exclude)
    {
        BoardManager bm = BoardManager.Instance;
        var result = new List<PieceInstance>();
        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int pos = new Vector2Int(center.x + dx, center.y + dy);
                if (!bm.IsInBounds(pos)) continue;
                PieceInstance piece = bm.GetPieceAt(pos);
                if (piece != null && piece.team == enemyTeam && piece.isAlive && piece != exclude
                    && piece.data.pieceType != PieceType.C3)
                    result.Add(piece);
            }
        }
        return result;
    }

    private void ShuffleList(List<PieceInstance> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            PieceInstance temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    // ============================================================
    // 中華・門人ヘルパー（既存）
    // ============================================================
    private IEnumerator SpawnChuka(Team team, int count)
    {
        BoardManager bm = BoardManager.Instance;
        PieceData chukaData = bm.GetPieceDataByType(PieceType.Chuka);
        if (chukaData == null) yield break;

        var emptyPositions = new List<Vector2Int>();
        for (int x = 0; x < bm.CurrentBoardSize; x++)
        {
            for (int y = 0; y < bm.CurrentBoardSize; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (bm.IsEmpty(pos))
                    emptyPositions.Add(pos);
            }
        }

        for (int i = 0; i < count && emptyPositions.Count > 0; i++)
        {
            int idx = Random.Range(0, emptyPositions.Count);
            Vector2Int spawnPos = emptyPositions[idx];
            emptyPositions.RemoveAt(idx);

            bm.SpawnPiece(chukaData, team, spawnPos);
            Debug.Log("中華が " + spawnPos + " に生成された！");

            yield return new WaitForSeconds(0.2f);
        }
    }

    private void HealAdjacentAllies(PieceInstance chuka)
    {
        BoardManager bm = BoardManager.Instance;
        Vector2Int center = chuka.boardPosition;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int pos = new Vector2Int(center.x + dx, center.y + dy);
                if (!bm.IsInBounds(pos)) continue;

                PieceInstance target = bm.GetPieceAt(pos);
                if (target != null && target.team == chuka.team && target != chuka)
                {
                    int maxHP = target.MaxHP;
                    if (target.currentHP < maxHP)
                    {
                        target.currentHP++;
                        PieceController pc = bm.GetPieceController(pos);
                        if (pc != null) pc.UpdateHP();
                        Debug.Log("中華が " + target.DisplayName + " を回復した！");
                    }
                }
            }
        }
    }

    // ============================================================
    // 自動移動（門人）
    // ============================================================
    private IEnumerator ExecuteAutoMove(PieceInstance piece)
    {
        List<MoveValidator.MoveResult> moves = MoveValidator.GetValidMoves(piece);
        if (moves.Count == 0) yield break;

        var attackMoves = new List<MoveValidator.MoveResult>();
        var normalMoves = new List<MoveValidator.MoveResult>();
        foreach (var m in moves)
        {
            if (m.isAttack)
            {
                // 味方駒への攻撃を除外（安全チェック）
                PieceInstance t = BoardManager.Instance.GetPieceAt(m.position);
                if (t != null && t.team != piece.team)
                    attackMoves.Add(m);
            }
            else
                normalMoves.Add(m);
        }

        MoveValidator.MoveResult chosen;
        if (attackMoves.Count > 0)
        {
            MoveValidator.MoveResult bestAttack = attackMoves[0];
            bool foundC3 = false;
            foreach (var m in attackMoves)
            {
                PieceInstance target = BoardManager.Instance.GetPieceAt(m.position);
                if (target != null && target.data.pieceType == PieceType.C3)
                {
                    bestAttack = m;
                    foundC3 = true;
                    break;
                }
            }
            if (!foundC3)
                bestAttack = attackMoves[Random.Range(0, attackMoves.Count)];
            chosen = bestAttack;
        }
        else
        {
            chosen = normalMoves[Random.Range(0, normalMoves.Count)];
        }

        ExecuteAutoMoveAction(piece, chosen);
        yield return new WaitForSeconds(0.3f);
    }

    private void ExecuteAutoMoveAction(PieceInstance piece, MoveValidator.MoveResult move)
    {
        BoardManager bm = BoardManager.Instance;
        Vector2Int from = piece.boardPosition;
        Vector2Int to = move.position;

        if (move.isAttack)
        {
            PieceInstance target = bm.GetPieceAt(to);
            if (target != null && target.team != piece.team)
            {
                // 挑発駒はダメージ無効（∞HP）
                if (target.data.isTauntPiece)
                {
                    if (BattleEffects.Instance != null)
                        BattleEffects.Instance.PlayHitEffect(to);
                    PieceController tpc = bm.GetPieceController(to);
                    if (tpc != null) tpc.Shake();
                    return;
                }

                int damage = Mathf.Max(0, piece.ATK - target.DEF);
                target.currentHP -= damage;

                PieceController targetPC = bm.GetPieceController(to);
                if (targetPC != null) targetPC.UpdateHP();

                if (target.currentHP <= 0)
                {
                    if (BattleEffects.Instance != null)
                        BattleEffects.Instance.PlayDefeatEffect(to);
                    int groupId = target.linkedGroupId;
                    bm.RemovePiece(to);
                    bm.RemovePieceController(to);
                    CheckLinkedDeaths(groupId);
                }
                else
                {
                    if (BattleEffects.Instance != null)
                        BattleEffects.Instance.PlayHitEffect(to);
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

        GameManager.Instance.CheckPromotion(piece);
    }

    // ============================================================
    // 僕バフシステム
    // ============================================================
    private IEnumerator ProcessBokuBuffs(Team team)
    {
        BoardManager bm = BoardManager.Instance;
        var bokuList = new List<PieceInstance>();
        var pieces = bm.GetTeamPieces(team);
        foreach (var p in pieces)
        {
            if (p.data.pieceType == PieceType.Boku && p.isAlive)
                bokuList.Add(p);
        }

        if (bokuList.Count == 0) yield break;

        var allies = bm.GetTeamPieces(team);
        foreach (var ally in allies)
        {
            if (ally.data.pieceType == PieceType.Boku || ally.data.pieceType == PieceType.C3) continue;
            if (!ally.isAlive) continue;

            bool nearBoku = false;
            bool nearPromotedBoku = false;
            foreach (var boku in bokuList)
            {
                int dx = Mathf.Abs(ally.boardPosition.x - boku.boardPosition.x);
                int dy = Mathf.Abs(ally.boardPosition.y - boku.boardPosition.y);
                int dist = Mathf.Max(dx, dy);
                int range = boku.isPromoted ? 2 : 1;
                if (dist <= range)
                {
                    nearBoku = true;
                    if (boku.isPromoted) nearPromotedBoku = true;
                }
            }

            if (nearBoku)
            {
                ally.turnsNearBoku++;
                if (ally.turnsNearBoku >= 2)
                {
                    int buffAmount = nearPromotedBoku ? 2 : 1;
                    bool buffATK = Random.value < 0.5f;
                    string statName;
                    if (buffATK)
                    {
                        ally.bonusATK += buffAmount;
                        statName = "ATK";
                    }
                    else
                    {
                        ally.bonusDEF += buffAmount;
                        statName = "DEF";
                    }
                    ally.turnsNearBoku = 0;

                    PieceController pc = bm.GetPieceController(ally.boardPosition);
                    if (pc != null) pc.GetRenderer().UpdateAllStats();

                    Debug.Log(ally.DisplayName + " に僕バフ！" + statName + "+" + buffAmount);
                    if (BattleLogUI.Instance != null)
                        BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(ally.DisplayName, ally.team) + " に" + statName + "+" + buffAmount + "バフ");
                }
            }
            else
            {
                ally.turnsNearBoku = 0;
            }
        }

        yield return null;
    }

    // ============================================================
    // 髑髏の死亡時爆発（隣接全駒に貫通1ダメージ）
    // ============================================================
    public void OnPieceDeath(PieceInstance piece, Vector2Int deathPos)
    {
        if (piece == null) return;
        if (piece.data.pieceType != PieceType.Dokuro) return;

        // 連鎖爆発防止
        if (explodingPositions.Contains(deathPos)) return;
        explodingPositions.Add(deathPos);

        BoardManager bm = BoardManager.Instance;

        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayDefeatEffect(deathPos);
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("髑髏", piece.team) + " が爆発！隣接駒に貫通1ダメージ");

        // 隣接全セルに貫通1ダメージ
        var victims = new List<PieceInstance>();
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int pos = new Vector2Int(deathPos.x + dx, deathPos.y + dy);
                if (!bm.IsInBounds(pos)) continue;
                PieceInstance target = bm.GetPieceAt(pos);
                if (target != null && target.isAlive)
                    victims.Add(target);
            }
        }

        foreach (var victim in victims)
        {
            if (!victim.isAlive) continue;
            DealPiercingDamage(victim, 1);
        }

        explodingPositions.Remove(deathPos);
    }
}
