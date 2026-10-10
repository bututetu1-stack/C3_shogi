using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>駒の能力。提督の艦隊は AbilitySystem.Fleet.cs</summary>
public partial class AbilitySystem : MonoBehaviour
{
    public static AbilitySystem Instance { get; private set; }

    private static int nextGroupId = 0;

    // 黄泉の召喚回数追跡
    private Dictionary<PieceInstance, int> yomigaeruSpawnCount = new Dictionary<PieceInstance, int>();

    // 髑髏の連鎖爆発防止
    private HashSet<Vector2Int> explodingPositions = new HashSet<Vector2Int>();

    // 軍将のATKバフの上限（1駒あたり）
    private const int MaxGundaishouStacks = 3;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 提督と艦娘のつながりを描く
        if (GetComponent<FleetLinkRenderer>() == null)
            gameObject.AddComponent<FleetLinkRenderer>();
    }

    /// <summary>ステージ開始時に能力の内部状態をリセットする</summary>
    public void ResetState()
    {
        yomigaeruSpawnCount.Clear();
        explodingPositions.Clear();
        ResetFleets();
    }

    // ============================================================
    // ターン開始時能力 (門人自動移動 + ヲツ中華生成)
    // ============================================================
    public IEnumerator ExecuteTurnStartAbilities(Team team)
    {
        BoardManager bm = BoardManager.Instance;

        // 艦隊の入渠（提督の隣の艦娘が回復）
        if (team == Team.Player) DockShips();

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
            int count = wotsu.isPromoted ? BalanceTuning.WotsuPromotedChukaCount : BalanceTuning.WotsuChukaCount;
            yield return SpawnChuka(wotsu, count);
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
                if (BattleLogUI.Instance != null)
                    BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(monin.DisplayName, monin.team) + " は絶起した！");
                SpeechBubble.Say(monin, PieceLines.MoninOversleep);
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
                        GameManager.Instance.ApplySummonBonuses(bm.GetPieceAt(spawnPos));
                        if (BattleEffects.Instance != null) BattleEffects.Instance.PlaySoulFire(spawnPos);
                        SpeechBubble.Say(yomi, PieceLines.YomigaeruSummon);
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
                    var source = GameSim.BeginSource(maou);
                    PieceInstance victim = targets[Random.Range(0, targets.Count)];
                    int lightning = CombatResolver.AbilityDamage(maou, 1, false);   // 局による強化は足さない
                    if (BattleLogUI.Instance != null)
                        BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("魔王", Team.Enemy) + " の雷撃！" + BattleLogUI.ColorName(victim.DisplayName, victim.team) + " に貫通" + lightning + "ダメージ");
                    if (BattleEffects.Instance != null) BattleEffects.Instance.PlayLightningEffect(victim.boardPosition);
                    yield return new WaitForSeconds(0.2f);
                    CombatResolver.ApplyDamage(victim, lightning, true);
                    GameSim.EndSource(source);
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

        // 冷笑されていた駒は、この手番を動けずに過ごしたので元に戻る
        foreach (var p in bm.GetTeamPieces(team))
        {
            if (!p.stunned) continue;
            p.stunned = false;
            CombatResolver.RefreshStats(p);
        }

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
            if (!chuka.isAlive) continue;
            HealAdjacentAllies(chuka, BalanceTuning.ChukaHeal, true);
            // 食べられて湯気とともに消える
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlaySteam(chuka.boardPosition);
            CombatResolver.RemoveWithExit(chuka, PieceController.ExitStyle.Eaten);
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
                HealAdjacentAllies(enmashi, 1);
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
                            && target.data.pieceType != PieceType.C3
                            && target.gundaishouStacks < MaxGundaishouStacks)
                        {
                            target.bonusATK += 1;
                            target.gundaishouStacks++;
                            CombatResolver.RefreshStats(target);
                            FloatingText.Spawn(target.boardPosition, "攻+1", Palette.ATK);
                            if (BattleEffects.Instance != null) BattleEffects.Instance.PlayBuffEffect(target.boardPosition, true);
                            buffed = true;
                        }
                    }
                }
                if (buffed && BattleLogUI.Instance != null)
                    BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("軍将", Team.Enemy) + " が味方を鼓舞！ATK+1");
                if (buffed) SpeechBubble.Say(gun, PieceLines.GundaishouRally);
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
            yield return SupportFleetFire();
        }

        // けい・異端のバグ修正、きぷ・へるの冷笑
        yield return ExecuteKeiAbilities(team);
        yield return ExecuteKipuAbilities(team);

        // 僕バフ処理
        yield return ProcessBokuBuffs(team);
    }

    /// <summary>team の生きている type の駒</summary>
    private static List<PieceInstance> AlivePieces(Team team, PieceType type)
    {
        var list = new List<PieceInstance>();
        foreach (var p in BoardManager.Instance.GetTeamPieces(team))
            if (p.data.pieceType == type && p.isAlive) list.Add(p);
        return list;
    }

    /// <summary>周囲1マスにいる敵（C3は能力の影響を受けないので除く）</summary>
    private static List<PieceInstance> AdjacentEnemies(PieceInstance center)
    {
        BoardManager bm = BoardManager.Instance;
        var list = new List<PieceInstance>();
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                PieceInstance target = bm.GetPieceAt(new Vector2Int(center.boardPosition.x + dx, center.boardPosition.y + dy));
                if (target != null && target.team != center.team && target.isAlive && target.data.pieceType != PieceType.C3)
                    list.Add(target);
            }
        }
        return list;
    }

    // ============================================================
    // けい／異端のバグ修正（自分の手番の終わりに、周囲1マスの敵を2体まで選び、それぞれの数値を1つ1下げる。
    // 異端は3体まで、それぞれの数値を2つ1ずつ下げる）
    // ============================================================
    private static readonly StatKind[] AllStats = { StatKind.ATK, StatKind.DEF, StatKind.HP };
    private static readonly Color BugGreen = new Color(0.36f, 1f, 0.6f);

    private IEnumerator ExecuteKeiAbilities(Team team)
    {
        foreach (var kei in AlivePieces(team, PieceType.Kei))
        {
            if (!kei.isAlive) continue;
            yield return ExecuteKeiAbility(kei);
        }
    }

    private IEnumerator ExecuteKeiAbility(PieceInstance kei)
    {
        // 下げられる数値が残っている敵だけ（攻撃・防御は0未満にせず、体力は0にしない）
        var targets = AdjacentEnemies(kei);
        targets.RemoveAll(target => LowerableStats(target).Count == 0);
        if (targets.Count == 0) yield break;
        ShuffleList(targets);

        SpeechBubble.Say(kei, kei.isPromoted ? PieceLines.ItanBug : PieceLines.KeiBug);
        RunRoster.Feat(kei);
        int targetCount = Mathf.Min(kei.isPromoted ? 3 : 2, targets.Count);
        int statCount = kei.isPromoted ? 2 : 1;
        for (int i = 0; i < targetCount; i++)
        {
            PieceInstance target = targets[i];
            if (!target.isAlive) continue;
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlayStream(kei.boardPosition, target.boardPosition, BugGreen);

            List<StatKind> stats = LowerableStats(target);
            for (int k = 0; k < statCount && stats.Count > 0; k++)
            {
                int idx = Random.Range(0, stats.Count);
                StatKind stat = stats[idx];
                stats.RemoveAt(idx);
                target.Lower(stat);

                string label = stat == StatKind.ATK ? "攻撃" : stat == StatKind.DEF ? "防御" : "体力";
                Color color = stat == StatKind.ATK ? Palette.ATK : stat == StatKind.DEF ? Palette.DEF : Palette.HP;
                FloatingText.Spawn(target.boardPosition, label.Substring(0, 1) + "-1", color, 3.2f, k * 0.25f);
                if (BattleLogUI.Instance != null)
                    BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(kei.DisplayName, kei.team) + " が " + BattleLogUI.ColorName(target.DisplayName, target.team) + " の" + label + "を1下げた");
            }
            CombatResolver.RefreshStats(target);
            yield return new WaitForSeconds(0.3f);
        }
    }

    private static List<StatKind> LowerableStats(PieceInstance piece)
    {
        var list = new List<StatKind>();
        foreach (StatKind stat in AllStats)
            if (piece.CanLower(stat)) list.Add(stat);
        return list;
    }

    // ============================================================
    // きぷ／へるの冷笑（自分の手番の終わりに、周囲1マスの敵を2体まで冷笑し、次の手番は動けなくする。へるは周りの敵すべて）
    // ============================================================
    private static readonly Color SneerBlue = new Color(0.62f, 0.85f, 1f);

    private IEnumerator ExecuteKipuAbilities(Team team)
    {
        foreach (var kipu in AlivePieces(team, PieceType.Kipu))
        {
            if (!kipu.isAlive) continue;
            yield return ExecuteKipuAbility(kipu);
        }
    }

    private IEnumerator ExecuteKipuAbility(PieceInstance kipu)
    {
        // もともと動けない駒（深海・中華など）や、もう冷笑された駒は相手にしない
        var targets = AdjacentEnemies(kipu);
        targets.RemoveAll(target => target.stunned || !CanEverMove(target));
        if (targets.Count == 0) yield break;
        if (!kipu.isPromoted)
        {
            ShuffleList(targets);
            if (targets.Count > BalanceTuning.KipuSneerTargets)
                targets.RemoveRange(BalanceTuning.KipuSneerTargets, targets.Count - BalanceTuning.KipuSneerTargets);
        }

        SpeechBubble.Say(kipu, PieceLines.KipuLaugh);
        RunRoster.Feat(kipu);
        foreach (var target in targets)
        {
            target.stunned = true;
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlayStream(kipu.boardPosition, target.boardPosition, SneerBlue);
            CombatResolver.RefreshStats(target);
            FloatingText.Spawn(target.boardPosition, "冷笑", SneerBlue);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(kipu.DisplayName, kipu.team) + " が " + BattleLogUI.ColorName(target.DisplayName, target.team) + " を冷笑した（次の手番は動けない）");
        }
        yield return new WaitForSeconds(0.3f);
    }

    private static bool CanEverMove(PieceInstance piece)
    {
        if (piece.data.isImmovable) return false;
        if (piece.isPromoted && piece.data.isImmovableWhenPromoted) return false;
        return piece.GetMoveDirections().Length > 0;
    }

    // ============================================================
    // 李白の裏返し能力（手動移動後に発動）
    // ============================================================
    public void ExecuteRihakuAbility(PieceInstance rihaku)
    {
        if (rihaku == null || !rihaku.isAlive || rihaku.data.pieceType != PieceType.Rihaku) return;

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

            if (!target.isAlive || !target.data.canPromote) continue;

            string beforeName = target.DisplayName;
            flipped++;
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("李白", rihaku.team) + "が " + BattleLogUI.ColorName(beforeName, target.team) + " を裏返した");

            if (target.isPromoted)
            {
                target.Demote();
                CombatResolver.PlayFlip(target);
            }
            else
            {
                // 成りに伴う特殊処理（SN・小錦の過労死など）も通常の成りと同じく発動
                GameManager.Instance.PromotePiece(target);
            }
            FloatingText.Spawn(target.boardPosition, "？", new Color(0.8f, 0.6f, 1f), 3.4f, 0.2f);
        }

        if (flipped > 0)
        {
            if (BattleEffects.Instance != null)
            {
                BattleEffects.Instance.PlayFlipSwirl(center, radius);
                BattleEffects.Instance.PlayFanSwirl(center, radius);
            }
            SpeechBubble.Say(rihaku, PieceLines.RihakuFlip);
            RunRoster.Feat(rihaku);
        }
    }

    // ============================================================
    // なこの能力（ドパ生成→突撃→パス上敵ダメージ）
    // ============================================================
    private static readonly Color NakoPink = new Color(1f, 0.45f, 0.75f);

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
        GameSim.AbilitySource = nako;
        yield return NakoAbilityBody(nako);
        GameSim.AbilitySource = null;
    }

    private IEnumerator NakoAbilityBody(PieceInstance nako)
    {
        BoardManager bm = BoardManager.Instance;

        // 能力中に成った場合も2回目が発動するよう、毎回回数を評価する
        for (int run = 0; run < (nako.isPromoted ? 2 : 1); run++)
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
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlaySparkle(dopaPos, NakoPink, Palette.GoldLight);
            if (run == 0) SpeechBubble.Say(nako, PieceLines.NakoDash);

            yield return new WaitForSeconds(0.3f);

            // なこからドパへの直線パスを取得（始点・終点含まない）
            List<Vector2Int> path = GetBresenhamLine(nakoPos, dopaPos);

            // なこをスライド移動（ボード上の移動はアニメ後に行う）
            PieceController nakoPC = bm.GetPieceController(nakoPos);
            if (nakoPC != null)
            {
                nakoPC.SetTrail(new Color(NakoPink.r, NakoPink.g, NakoPink.b, 0.55f), 0.5f);
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayDashStreak(nakoPos, dopaPos, NakoPink, 0.4f);
                yield return nakoPC.SlideToCoroutine(dopaPos, 0.4f);
            }

            if (!nako.isAlive) yield break;

            if (BattleEffects.Instance != null)
                BattleEffects.Instance.PlayMoveEffect();

            // なこ到着後にドパを撃破エフェクト付きで除去
            PieceInstance dopaInstance = bm.GetPieceAt(dopaPos);
            if (dopaInstance != null && dopaInstance.data.pieceType == PieceType.Dopa)
            {
                // ドーパミンがはじける
                if (BattleEffects.Instance != null)
                    BattleEffects.Instance.PlaySparkle(dopaPos, NakoPink, Palette.GoldLight);
                bm.RemovePieceController(dopaPos);
                bm.RemovePiece(dopaPos);
            }

            // ボード上でなこを移動（ドパの位置に他の駒が来ていたら元の位置に戻す）
            if (bm.IsEmpty(dopaPos))
                CombatResolver.MovePieceTo(nako, dopaPos);
            else if (nakoPC != null)
                nakoPC.MoveTo(nakoPos);

            // パス上の敵駒に2ダメージ（スライド到着後。攻撃の上乗せぶん増える）。C3は能力の影響を受けない
            int dash = CombatResolver.AbilityDamage(nako, 2);
            bool hitAny = false;
            foreach (var pos in path)
            {
                PieceInstance target = bm.GetPieceAt(pos);
                if (target != null && target.team != nako.team && target.isAlive
                    && target.data.pieceType != PieceType.C3)
                {
                    hitAny = true;
                    if (BattleLogUI.Instance != null)
                        BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("なこ", nako.team) + "の突撃！" + BattleLogUI.ColorName(target.DisplayName, target.team) + " に" + dash + "ダメージ");
                    CombatResolver.ApplyDamage(target, dash, false);
                    yield return new WaitForSeconds(0.15f);
                }
            }

            if (hitAny) RunRoster.Feat(nako);

            // 成りチェック
            yield return GameManager.Instance.CheckPromotionRoutine(nako);

            yield return new WaitForSeconds(0.3f);
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
    private IEnumerator SpawnChuka(PieceInstance cook, int count)
    {
        BoardManager bm = BoardManager.Instance;
        PieceData chukaData = bm.GetPieceDataByType(PieceType.Chuka);
        if (chukaData == null) yield break;
        Team team = cook.team;
        Vector3 cookPos = new Vector3(cook.boardPosition.x, cook.boardPosition.y, 0f);
        SpeechBubble.Say(cook, PieceLines.WotsuCook);
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayWokFlame(cook.boardPosition);
        yield return new WaitForSeconds(0.2f);

        // 味方のそばに置かないときは、盤の空きマスから重ならないように選ぶ
        List<Vector2Int> anywhere = BalanceTuning.ChukaNearAllies ? null : EmptySquares();
        for (int i = 0; i < count; i++)
        {
            Vector2Int? picked;
            if (anywhere != null)
            {
                if (anywhere.Count == 0) break;
                int idx = Random.Range(0, anywhere.Count);
                picked = anywhere[idx];
                anywhere.RemoveAt(idx);
            }
            else picked = PickChukaSpot(team);
            if (!picked.HasValue) break;
            Vector2Int spawnPos = picked.Value;

            // ヲツのところから放り投げられて、湯気を立てて着地する
            PieceController pc = bm.SpawnPiece(chukaData, team, spawnPos);
            if (pc != null) pc.FlyFrom(cookPos, 0.35f);
            PieceInstance chuka = bm.GetPieceAt(spawnPos);
            if (chuka != null) chuka.summoner = cook;
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlaySteam(spawnPos, 0.3f);

            yield return new WaitForSeconds(0.25f);
        }
    }

    /// <summary>
    /// 中華を置く場所: 味方のそばの空きマスから選ぶ（傷ついた味方のそばほど選ばれやすい）。
    /// そばに空きがなければ盤のどこか
    /// </summary>
    private Vector2Int? PickChukaSpot(Team team)
    {
        BoardManager bm = BoardManager.Instance;
        int size = bm.CurrentBoardSize;
        var spots = new List<Vector2Int>();
        var weights = new List<int>();
        var anyEmpty = new List<Vector2Int>();
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (!bm.IsEmpty(pos)) continue;
                anyEmpty.Add(pos);
                int weight = 0;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        PieceInstance n = bm.GetPieceAt(new Vector2Int(x + dx, y + dy));
                        if (n == null || n.team != team || n.data.pieceType == PieceType.Chuka) continue;
                        weight += n.currentHP < n.MaxHP ? 3 : 1;
                    }
                }
                if (weight > 0)
                {
                    spots.Add(pos);
                    weights.Add(weight);
                }
            }
        }
        if (spots.Count == 0)
            return anyEmpty.Count > 0 ? anyEmpty[Random.Range(0, anyEmpty.Count)] : (Vector2Int?)null;

        int total = 0;
        foreach (int w in weights) total += w;
        int roll = Random.Range(0, total);
        for (int i = 0; i < spots.Count; i++)
        {
            roll -= weights[i];
            if (roll < 0) return spots[i];
        }
        return spots[spots.Count - 1];
    }

    /// <summary>盤の空きマス（左の列から順に）</summary>
    private static List<Vector2Int> EmptySquares()
    {
        BoardManager bm = BoardManager.Instance;
        var list = new List<Vector2Int>();
        for (int x = 0; x < bm.CurrentBoardSize; x++)
        {
            for (int y = 0; y < bm.CurrentBoardSize; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (bm.IsEmpty(pos)) list.Add(pos);
            }
        }
        return list;
    }

    /// <summary>周りの味方を回復する。feed=true（中華）なら食べた味方は「満腹」で攻撃+1（C3・中華を除く、1体に上限あり）</summary>
    private void HealAdjacentAllies(PieceInstance chuka, int amount, bool feed = false)
    {
        BoardManager bm = BoardManager.Instance;
        Vector2Int center = chuka.boardPosition;
        bool helped = false;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int pos = new Vector2Int(center.x + dx, center.y + dy);
                if (!bm.IsInBounds(pos)) continue;

                PieceInstance target = bm.GetPieceAt(pos);
                if (target == null || target.team != chuka.team || target == chuka) continue;
                if (CombatResolver.Heal(target, amount) > 0) helped = true;
                if (feed && target.data.pieceType != PieceType.C3 && target.data.pieceType != PieceType.Chuka
                    && target.fullCount < BalanceTuning.ChukaFullMax)
                {
                    target.fullCount++;
                    target.bonusATK += 1;
                    helped = true;
                    CombatResolver.RefreshStats(target);
                    FloatingText.Spawn(target.boardPosition, "満腹 攻+1", Palette.ATK, 2.8f, 0.3f);
                }
            }
        }
        // 中華が役に立ったら、作ったヲツの活躍
        if (feed && helped && chuka.summoner != null) RunRoster.Feat(chuka.summoner);
    }

    /// <summary>小錦が攻撃されたとき、周りの味方が「同情」して攻撃+1（C3・小錦を除く、1体に上限あり）</summary>
    public void Sympathize(PieceInstance konishiki)
    {
        BoardManager bm = BoardManager.Instance;
        bool any = false;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                PieceInstance ally = bm.GetPieceAt(new Vector2Int(konishiki.boardPosition.x + dx, konishiki.boardPosition.y + dy));
                if (ally == null || ally.team != konishiki.team || !ally.isAlive) continue;
                if (ally.data.pieceType == PieceType.C3 || ally.data.pieceType == PieceType.Konishiki) continue;
                if (ally.sympathyCount >= BalanceTuning.KonishikiSympathyMax) continue;
                ally.sympathyCount++;
                ally.bonusATK += 1;
                CombatResolver.RefreshStats(ally);
                FloatingText.Spawn(ally.boardPosition, "同情 攻+1", Palette.ATK, 2.8f, 0.2f);
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayBuffEffect(ally.boardPosition, true);
                any = true;
            }
        }
        if (any && BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("小錦", konishiki.team) + " がいじめられて、周りが奮起した（攻撃+1）");
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

        // 門人は叫びながら突き進む（残像つき）
        SpeechBubble.Say(piece, PieceLines.MoninRush);
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayRage(piece.boardPosition, piece.isPromoted);
        PieceController pc = BoardManager.Instance.GetPieceController(piece.boardPosition);
        if (pc != null) pc.SetTrail(new Color(1f, 0.4f, 0.3f, 0.5f), 0.6f);
        yield return new WaitForSeconds(0.25f);

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

        if (chosen.isAttack) RunRoster.Feat(piece);
        yield return CombatResolver.ExecuteMove(piece, chosen);
        yield return new WaitForSeconds(0.3f);
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

        var allies = bm.GetTeamPieces(team);
        var cheering = new List<PieceInstance>();   // 誰かを強くした僕

        // 僕がいなければカウンタをリセット
        if (bokuList.Count == 0)
        {
            foreach (var ally in allies) ally.turnsNearBoku = 0;
            yield break;
        }
        foreach (var ally in allies)
        {
            if (ally.data.pieceType == PieceType.Boku || ally.data.pieceType == PieceType.C3) continue;
            if (!ally.isAlive) continue;

            bool nearBoku = false;
            bool nearPromotedBoku = false;
            PieceInstance mentor = null;
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
                    if (mentor == null || boku.isPromoted) mentor = boku;
                }
            }

            if (nearBoku)
            {
                ally.turnsNearBoku++;
                if (ally.turnsNearBoku >= BalanceTuning.BokuBuffTurns)
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
                    CombatResolver.RefreshStats(ally);
                    if (mentor != null && !cheering.Contains(mentor)) cheering.Add(mentor);
                    if (mentor != null && BattleEffects.Instance != null) BattleEffects.Instance.PlayMentorBeam(mentor.boardPosition, ally.boardPosition);
                    FloatingText.Spawn(ally.boardPosition, (buffATK ? "攻+" : "防+") + buffAmount, buffATK ? Palette.ATK : Palette.DEF);
                    if (BattleEffects.Instance != null) BattleEffects.Instance.PlayBuffEffect(ally.boardPosition, buffATK);

                    if (BattleLogUI.Instance != null)
                        BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(ally.DisplayName, ally.team) + " に" + statName + "+" + buffAmount + "バフ");
                }
            }
            else
            {
                ally.turnsNearBoku = 0;
            }
        }

        // 後方で腕組みをして頷く
        foreach (var boku in cheering)
        {
            RunRoster.Feat(boku);
            SpeechBubble.Say(boku, boku.isPromoted ? PieceLines.BokuCheerPromoted : PieceLines.BokuCheer);
            PieceController pc = bm.GetPieceController(boku.boardPosition);
            if (pc != null) pc.Nod();
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

        int blast = CombatResolver.AbilityDamage(piece, 1);
        if (BattleEffects.Instance != null)
            BattleEffects.Instance.PlayExplosionEffect(deathPos);
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("髑髏", piece.team) + " が爆発！隣接駒に貫通" + blast + "ダメージ");

        // 隣接全セルに貫通1ダメージ（攻撃の上乗せぶん増える。C3は能力の影響を受けない）
        var victims = new List<PieceInstance>();
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int pos = new Vector2Int(deathPos.x + dx, deathPos.y + dy);
                if (!bm.IsInBounds(pos)) continue;
                PieceInstance target = bm.GetPieceAt(pos);
                if (target != null && target.isAlive && target.data.pieceType != PieceType.C3)
                    victims.Add(target);
            }
        }

        var source = GameSim.BeginSource(piece);
        foreach (var victim in victims)
            CombatResolver.ApplyDamage(victim, blast, true);
        GameSim.EndSource(source);

        explodingPositions.Remove(deathPos);
    }
}
