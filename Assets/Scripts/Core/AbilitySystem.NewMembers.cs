using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 2026-10-11 に加わった部員の能力: 光晴（裏帳簿・高飛び）、ニコ（タスク処理）、〆鯖（正論パンチ・レスバ・世界が終わる）、翡翠（ジャンク漁り・最強PC）
/// </summary>
public partial class AbilitySystem
{
    private static readonly Color FundGold = new Color(1f, 0.82f, 0.35f);
    private static readonly Color TaskOrange = new Color(1f, 0.72f, 0.42f);
    private static readonly Color ArgueWhite = new Color(0.9f, 0.94f, 1f);
    private static readonly Color JadeGreen = new Color(0.36f, 0.88f, 0.78f);

    /// <summary>高飛びして盤外にいる光晴（その局のあいだ）</summary>
    private readonly List<PieceInstance> abroad = new List<PieceInstance>();
    /// <summary>ニコの山札（投げられたタスク。古い順）</summary>
    private readonly Dictionary<PieceInstance, List<NikoTask>> nikoDecks = new Dictionary<PieceInstance, List<NikoTask>>();
    /// <summary>〆鯖の「世界が終わる」までの手番</summary>
    private readonly Dictionary<PieceInstance, int> doomCountdown = new Dictionary<PieceInstance, int>();
    /// <summary>翡翠が集めたパーツ（その局のあいだ）</summary>
    private readonly Dictionary<PieceInstance, int> junkParts = new Dictionary<PieceInstance, int>();
    /// <summary>翡翠が「誘うと来てくれる」で動いた回数（その局のあいだ）</summary>
    private readonly Dictionary<PieceInstance, int> invitesUsed = new Dictionary<PieceInstance, int>();
    /// <summary>ニコの札・翡翠のオーバークロックで上がった攻撃の量（1体に上限をつける）</summary>
    private readonly Dictionary<PieceInstance, int> smallBuffs = new Dictionary<PieceInstance, int>();

    private struct NikoTask
    {
        public PieceType type;
        public PieceInstance thrower;   // 自分で引いたときは null
    }

    private void ResetNewMembers()
    {
        abroad.Clear();
        nikoDecks.Clear();
        doomCountdown.Clear();
        junkParts.Clear();
        smallBuffs.Clear();
        invitesUsed.Clear();
    }

    /// <summary>盤外にいる自軍の駒（局に勝ったときの練度に数える）</summary>
    public List<PieceInstance> AbroadPieces { get { return abroad; } }

    // ============================================================
    // 駒の情報に出す状態（裏金・タスク・世界が終わるまで・パーツ）
    // ============================================================
    public static string StatusLine(PieceInstance p)
    {
        if (p == null || Instance == null) return null;
        switch (p.data.pieceType)
        {
            case PieceType.Mitsuharu:
                return GameManager.Instance != null ? "裏金 " + GameManager.Instance.SecretFund + "/" + BalanceTuning.MitsuharuFundMax : null;
            case PieceType.Niko:
            {
                List<NikoTask> deck;
                int n = Instance.nikoDecks.TryGetValue(p, out deck) ? deck.Count : 0;
                return "タスク " + n + "/" + NikoCapacity(p) + "（1手番に" + NikoPerTurn(p) + "枚処理）";
            }
            case PieceType.Shimesaba:
            {
                int left;
                return p.isPromoted && Instance.doomCountdown.TryGetValue(p, out left) ? "世界が終わるまで あと" + left + "手番" : null;
            }
            case PieceType.Kawasemi:
                return "パーツ " + Instance.PartsOf(p) + "/" + BalanceTuning.KawasemiPartsMax
                    + (p.awakened ? "（最強PC: 当たりしか出ない）" : p.isPromoted ? "（当たり " + Mathf.RoundToInt(KawasemiGoodChance(Instance.PartsOf(p)) * 100f) + "%）" : "");
            default:
                return null;
        }
    }

    // ============================================================
    // 撃破のたびに: 光晴の裏金、翡翠のパーツ
    // ============================================================
    public void OnPieceKilled(PieceInstance victim, Vector2Int pos)
    {
        if (victim == null) return;
        BoardManager bm = BoardManager.Instance;
        PieceType v = victim.data.pieceType;
        bool fodder = v == PieceType.Chuka || v == PieceType.Dopa;

        // 裏帳簿: 盤にいる光晴（成る前）がいれば、味方が敵を倒すたびに裏金がたまる
        if (victim.team == Team.Enemy && !fodder && GameManager.Instance != null)
        {
            foreach (var m in AlivePieces(Team.Player, PieceType.Mitsuharu))
            {
                if (m.isPromoted || GameManager.Instance.SecretFund >= BalanceTuning.MitsuharuFundMax) break;
                GameManager.Instance.SecretFund += BalanceTuning.MitsuharuFundPerKill;
                GameManager.Instance.SecretFund = Mathf.Min(GameManager.Instance.SecretFund, BalanceTuning.MitsuharuFundMax);
                FloatingText.Spawn(m.boardPosition, "裏金+" + BalanceTuning.MitsuharuFundPerKill, FundGold, 2.8f, 0.2f);
                RunRoster.Feat(m);
                break;
            }
        }

        // ジャンク漁り: 周り2マスで駒が倒れたらパーツを拾う（敵味方を問わない）
        if (fodder) return;
        foreach (var team in new[] { Team.Player, Team.Enemy })
        {
            foreach (var k in AlivePieces(team, PieceType.Kawasemi))
            {
                if (k == victim) continue;
                if (Mathf.Max(Mathf.Abs(k.boardPosition.x - pos.x), Mathf.Abs(k.boardPosition.y - pos.y)) > BalanceTuning.KawasemiScavengeRange) continue;
                int parts = PartsOf(k);
                if (parts >= BalanceTuning.KawasemiPartsMax) continue;
                junkParts[k] = parts + 1;
                FloatingText.Spawn(k.boardPosition, "パーツ+1", JadeGreen, 2.8f, 0.3f);
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayStream(pos, k.boardPosition, JadeGreen);
                RunRoster.Feat(k);
            }
        }
    }

    private int PartsOf(PieceInstance k)
    {
        int n;
        return junkParts.TryGetValue(k, out n) ? n : 0;
    }

    // ============================================================
    // 光晴: 高飛び（成ると盤外へ）とお土産
    // ============================================================

    /// <summary>成った光晴を盤外へ送る（撃破ではないので数えない。攻撃もされない）</summary>
    public void SendAbroad(PieceInstance m)
    {
        if (m == null || !m.isAlive) return;
        Vector2Int pos = m.boardPosition;
        FloatingText.Spawn(pos, "高飛び", FundGold, 3.8f);
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayTakeoff(pos);
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("光晴", m.team) + " は裏金を持って海外旅行へ旅立った（裏金 " + (GameManager.Instance != null ? GameManager.Instance.SecretFund : 0) + "）");
        CombatResolver.RemoveWithExit(m, PieceController.ExitStyle.Retreat);
        abroad.Add(m);
    }

    /// <summary>自軍の手番の初めに、旅費（裏金）がたまった光晴は海外へ旅立ち、盤外の光晴は裏金を使って味方1体にお土産を送る</summary>
    private IEnumerator SendSouvenirs(Team team)
    {
        if (team != Team.Player || GameManager.Instance == null) yield break;
        foreach (var m in AlivePieces(team, PieceType.Mitsuharu))
        {
            if (m.isPromoted) continue;
            // 会計の権力: 盤にいるあいだ、手番ごとに裏金がたまる
            if (GameManager.Instance.SecretFund < BalanceTuning.MitsuharuFundMax && BalanceTuning.MitsuharuFundPerTurn > 0)
            {
                GameManager.Instance.SecretFund = Mathf.Min(BalanceTuning.MitsuharuFundMax, GameManager.Instance.SecretFund + BalanceTuning.MitsuharuFundPerTurn);
                FloatingText.Spawn(m.boardPosition, "裏金+" + BalanceTuning.MitsuharuFundPerTurn, FundGold, 2.6f);
            }
            if (GameManager.Instance.SecretFund < BalanceTuning.MitsuharuTripCost) continue;
            FloatingText.Spawn(m.boardPosition, "旅費がたまった", FundGold, 3.2f);
            yield return GameManager.Instance.PromoteRoutine(m);
        }
        if (abroad.Count == 0) yield break;
        foreach (var m in abroad)
        {
            if (GameManager.Instance.SecretFund <= 0) break;
            // お土産は部員を優先（傷ついた部員 → 部員 → ほかの駒）
            var allies = new List<PieceInstance>();
            var members = new List<PieceInstance>();
            var hurt = new List<PieceInstance>();
            foreach (var p in BoardManager.Instance.GetTeamPieces(team))
            {
                PieceType t = p.data.pieceType;
                if (!p.isAlive || t == PieceType.C3 || t == PieceType.Chuka || t == PieceType.Dopa) continue;
                allies.Add(p);
                if (!IsMemberType(t)) continue;
                members.Add(p);
                if (p.currentHP < p.MaxHP) hurt.Add(p);
            }
            if (hurt.Count >= (m.awakened ? BalanceTuning.MitsuharuAwakenedTargets : BalanceTuning.MitsuharuSouvenirTargets)) allies = hurt;
            else if (members.Count > 0)
            {
                foreach (var h in hurt) members.Remove(h);
                ShuffleList(members);
                var others = new List<PieceInstance>();
                foreach (var a in allies) if (!IsMemberType(a.data.pieceType)) others.Add(a);
                ShuffleList(others);
                hurt.AddRange(members);
                hurt.AddRange(others);   // 部員が足りなければ、ほかの駒にも配る
                allies = hurt;
            }
            if (allies.Count == 0) yield break;
            GameManager.Instance.SecretFund--;
            int gifts = m.awakened ? BalanceTuning.MitsuharuAwakenedTargets : BalanceTuning.MitsuharuSouvenirTargets;
            if (m.awakened && BattleEffects.Instance != null) BattleEffects.Instance.PlayAwakenBurst("WorldTrip", allies[0].boardPosition, m.data.awakenColor, true);
            for (int g = 0; g < gifts && allies.Count > 0; g++)
            {
                int pick = allies == hurt ? 0 : Random.Range(0, allies.Count);
                PieceInstance to = allies[pick];
                allies.RemoveAt(pick);
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlaySouvenir(to.boardPosition);
                yield return new WaitForSeconds(0.3f);
                string gift;
                if (to.currentHP < to.MaxHP)
                {
                    CombatResolver.Heal(to, BalanceTuning.MitsuharuSouvenirHeal);
                    gift = "体力+" + BalanceTuning.MitsuharuSouvenirHeal;
                }
                else
                {
                    to.bonusATK += 1;
                    CombatResolver.RefreshStats(to);
                    FloatingText.Spawn(to.boardPosition, "攻+1", Palette.ATK, 3f);
                    gift = "攻撃+1";
                }
                FloatingText.Spawn(to.boardPosition, "お土産", FundGold, 3.2f, 0.25f);
                RunRoster.Feat(m);
                if (BattleLogUI.Instance != null)
                    BattleLogUI.Instance.AddLog("海外の" + BattleLogUI.ColorName("光晴", m.team) + " から " + BattleLogUI.ColorName(to.DisplayName, to.team) + " にお土産（" + gift + "、裏金 残り" + GameManager.Instance.SecretFund + "）");
            }
            yield return new WaitForSeconds(0.3f);
        }

        // 裏金が尽きたら帰国する（成る前の姿で、自陣に戻ってくる）
        if (GameManager.Instance.SecretFund > 0 || !BalanceTuning.MitsuharuReturnsWhenBroke) yield break;
        foreach (var m in new List<PieceInstance>(abroad))
        {
            Vector2Int? home = HomeSquare(team);
            if (!home.HasValue) break;
            abroad.Remove(m);
            BoardManager bm = BoardManager.Instance;
            PieceController pc = bm.SpawnPiece(m.data, team, home.Value);
            PieceInstance back = bm.GetPieceAt(home.Value);
            if (back == null) continue;
            GameManager.Instance.ApplySummonBonuses(back);
            if (pc != null) pc.FlyFrom(new Vector3(home.Value.x + 2.5f, home.Value.y + 6f, 0f), 0.5f);
            FloatingText.Spawn(home.Value, "帰国", FundGold, 3.6f, 0.3f);
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlaySouvenir(home.Value);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("光晴", team) + " は裏金を使い果たして帰国した");
            yield return new WaitForSeconds(0.4f);
        }
    }

    /// <summary>自陣の手前（C3 に近い段）から空きマスを選ぶ</summary>
    private static Vector2Int? HomeSquare(Team team)
    {
        BoardManager bm = BoardManager.Instance;
        int size = bm.CurrentBoardSize;
        for (int row = 0; row < size; row++)
        {
            int y = team == Team.Player ? row : size - 1 - row;
            var empty = new List<Vector2Int>();
            for (int x = 0; x < size; x++)
                if (bm.IsEmpty(new Vector2Int(x, y))) empty.Add(new Vector2Int(x, y));
            if (empty.Count > 0) return empty[Random.Range(0, empty.Count)];
        }
        return null;
    }

    // ============================================================
    // ニコ: タスクを投げる × デッキ構築
    // ============================================================
    private static int NikoCapacity(PieceInstance niko) { return niko.isPromoted ? BalanceTuning.NikoPromotedCapacity : BalanceTuning.NikoCapacity; }
    private static int NikoPerTurn(PieceInstance niko) { return (niko.isPromoted ? 2 : 1) + (niko.awakened ? BalanceTuning.NikoAwakenedExtraTasks : 0); }
    /// <summary>覚醒したニコの札は、ダメージ・回復・裏金・強化の量が増える</summary>
    private static int CardBonus(PieceInstance niko) { return niko.awakened ? BalanceTuning.NikoAwakenedCardBonus : 0; }

    /// <summary>ニコの札になる部員か（能力のない駒・召喚物・艦娘は札にならない）</summary>
    private static bool HasCard(PieceType t)
    {
        switch (t)
        {
            case PieceType.Wotsu: case PieceType.Kei: case PieceType.Kipu: case PieceType.Boku: case PieceType.Rihaku:
            case PieceType.Nako: case PieceType.Monin: case PieceType.Konishiki: case PieceType.Yuu: case PieceType.SN:
            case PieceType.Monotetsu: case PieceType.Mitsuharu: case PieceType.Shimesaba: case PieceType.Kawasemi:
                return true;
            default:
                return false;
        }
    }

    private IEnumerator ExecuteNikoAbilities(Team team)
    {
        foreach (var niko in AlivePieces(team, PieceType.Niko))
        {
            if (!niko.isAlive) continue;
            List<NikoTask> deck;
            if (!nikoDecks.TryGetValue(niko, out deck)) { deck = new List<NikoTask>(); nikoDecks[niko] = deck; }

            // 隣（成ると2マス先まで）の部員がタスクを投げてくる
            int range = niko.isPromoted ? 2 : 1;
            int thrown = 0;
            foreach (var p in BoardManager.Instance.GetTeamPieces(team))
            {
                if (deck.Count >= NikoCapacity(niko)) break;
                if (p == niko || !p.isAlive || !HasCard(p.data.pieceType)) continue;
                if (Mathf.Max(Mathf.Abs(p.boardPosition.x - niko.boardPosition.x), Mathf.Abs(p.boardPosition.y - niko.boardPosition.y)) > range) continue;
                deck.Add(new NikoTask { type = p.data.pieceType, thrower = p });
                thrown++;
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayStream(p.boardPosition, niko.boardPosition, TaskOrange);
            }
            if (thrown > 0)
            {
                FloatingText.Spawn(niko.boardPosition, "タスク+" + thrown, TaskOrange, 3f);
                yield return new WaitForSeconds(0.25f);
            }

            // 1手番に1枚（成ると2枚）処理する。山札の古い順に、いまできる札を使う（相手がいないなど、できない札は片付ける）。
            // 山札が空になったら、仲間の部員の札を引いて、できるものを使う
            for (int i = 0; i < NikoPerTurn(niko) && niko.isAlive; i++)
            {
                bool done = false;
                while (!done && deck.Count > 0)
                {
                    NikoTask task = deck[0];
                    deck.RemoveAt(0);
                    done = TryTask(niko, task);
                }
                if (!done)
                {
                    foreach (PieceType drawn in DrawMemberCards(team))
                    {
                        done = TryTask(niko, new NikoTask { type = drawn, thrower = null });
                        if (done) break;
                    }
                }
                if (!done)
                {
                    if (i == 0 && BattleLogUI.Instance != null)
                        BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("ニコ", niko.team) + " はいまできるタスクがなかった");
                    break;
                }
                yield return new WaitForSeconds(0.35f);
            }
        }
    }

    /// <summary>暇なとき: 仲間にした部員の札を、引く順に並べる（ニコ自身は除く。同じ部員は1枚）</summary>
    private static List<PieceType> DrawMemberCards(Team team)
    {
        var pool = new List<PieceType>();
        if (team != Team.Player || GameManager.Instance == null) return pool;
        foreach (var d in GameManager.Instance.OwnedPieces)
            if (d != null && HasCard(d.pieceType) && !pool.Contains(d.pieceType)) pool.Add(d.pieceType);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            PieceType t = pool[i]; pool[i] = pool[j]; pool[j] = t;
        }
        return pool;
    }

    /// <summary>札を1枚使ってみる。できなければ何もせず false（相手がいない・回復する味方がいない・上限に届いている など）</summary>
    private bool TryTask(PieceInstance niko, NikoTask task)
    {
        GameSim.AbilitySource = niko;
        bool done = DoCard(niko, task);
        GameSim.AbilitySource = null;
        if (!done) return false;

        string name = MemberName(task.type);
        FloatingText.Spawn(niko.boardPosition, (task.thrower != null ? "処理: " : "ドロー: ") + name, TaskOrange, 3.2f, 0.15f);
        if (niko.awakened && BattleEffects.Instance != null) BattleEffects.Instance.PlayAwakenBurst("CardFan", niko.boardPosition, niko.data.awakenColor, true);
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("ニコ", niko.team) + (task.thrower != null ? " が" + name + "のタスクを処理" : " が" + name + "の札を引いて使った"));
        RunRoster.Feat(niko);
        // いつもありがとうございます: 処理したタスクは投げた部員の活躍になる
        if (task.thrower != null && task.thrower.isAlive) RunRoster.Feat(task.thrower);
        return true;
    }

    private static string MemberName(PieceType t)
    {
        PieceData d = BoardManager.Instance != null ? BoardManager.Instance.GetPieceDataByType(t) : null;
        return d != null ? d.displayName : t.ToString();
    }

    /// <summary>札の効果（その部員の能力を、ニコの場所から小さく1回使う）。何か起きたら true</summary>
    private bool DoCard(PieceInstance niko, NikoTask task)
    {
        BoardManager bm = BoardManager.Instance;
        Team foe = niko.team == Team.Player ? Team.Enemy : Team.Player;
        switch (task.type)
        {
            case PieceType.Wotsu:
            {
                // 中華を1つ（食べられたら投げたヲツの活躍）
                PieceData chukaData = bm.GetPieceDataByType(PieceType.Chuka);
                Vector2Int? spot = PickChukaSpot(niko.team);
                if (chukaData == null || !spot.HasValue) return false;
                PieceController pc = bm.SpawnPiece(chukaData, niko.team, spot.Value);
                if (pc != null) pc.FlyFrom(new Vector3(niko.boardPosition.x, niko.boardPosition.y, 0f), 0.35f);
                PieceInstance chuka = bm.GetPieceAt(spot.Value);
                if (chuka != null) chuka.summoner = task.thrower != null ? task.thrower : niko;
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlaySteam(spot.Value, 0.3f);
                // 覚醒: もう1皿
                if (CardBonus(niko) > 0)
                {
                    Vector2Int? more = PickChukaSpot(niko.team);
                    if (more.HasValue)
                    {
                        bm.SpawnPiece(chukaData, niko.team, more.Value);
                        PieceInstance extra = bm.GetPieceAt(more.Value);
                        if (extra != null) extra.summoner = chuka != null ? chuka.summoner : niko;
                    }
                }
                return true;
            }
            case PieceType.Kei:
            {
                var targets = AdjacentEnemies(niko);
                targets.RemoveAll(t => LowerableStats(t).Count == 0);
                if (targets.Count == 0) return false;
                PieceInstance target = targets[Random.Range(0, targets.Count)];
                List<StatKind> stats = LowerableStats(target);
                StatKind stat = stats[Random.Range(0, stats.Count)];
                target.Lower(stat);
                // 覚醒: もう1つ下げる
                for (int extra = 0; extra < CardBonus(niko); extra++)
                {
                    List<StatKind> rest = LowerableStats(target);
                    rest.Remove(stat);
                    if (rest.Count > 0) target.Lower(rest[Random.Range(0, rest.Count)]);
                }
                CombatResolver.RefreshStats(target);
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayStream(niko.boardPosition, target.boardPosition, BugGreen);
                FloatingText.Spawn(target.boardPosition, (stat == StatKind.ATK ? "攻" : stat == StatKind.DEF ? "防" : "体") + "-1",
                    stat == StatKind.ATK ? Palette.ATK : stat == StatKind.DEF ? Palette.DEF : Palette.HP, 3.2f);
                return true;
            }
            case PieceType.Kipu:
            {
                var targets = AdjacentEnemies(niko);
                targets.RemoveAll(t => t.stunned || !CanEverMove(t));
                if (targets.Count == 0) return false;
                PieceInstance target = targets[Random.Range(0, targets.Count)];
                target.stunned = true;
                CombatResolver.RefreshStats(target);
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayStream(niko.boardPosition, target.boardPosition, SneerBlue);
                FloatingText.Spawn(target.boardPosition, "冷笑", SneerBlue);
                return true;
            }
            case PieceType.Boku:
            {
                // 周りの味方1体の、攻撃と防御の低いほう+1（1体に上限あり）
                var allies = AdjacentAllies(niko);
                allies.RemoveAll(a => SmallBuffOf(a) >= BalanceTuning.NikoBuffCap + CardBonus(niko));
                if (allies.Count == 0) return false;
                PieceInstance ally = allies[Random.Range(0, allies.Count)];
                bool atk = ally.ATK <= ally.DEF;
                if (atk) ally.bonusATK += 1; else ally.bonusDEF += 1;
                smallBuffs[ally] = SmallBuffOf(ally) + 1;
                CombatResolver.RefreshStats(ally);
                FloatingText.Spawn(ally.boardPosition, atk ? "攻+1" : "防+1", atk ? Palette.ATK : Palette.DEF);
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayBuffEffect(ally.boardPosition, atk);
                return true;
            }
            case PieceType.Rihaku:
            {
                // 得になる駒だけ1枚裏返す（詩仙と同じ選び方）
                var candidates = new List<PieceInstance>();
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        PieceInstance t = bm.GetPieceAt(new Vector2Int(niko.boardPosition.x + dx, niko.boardPosition.y + dy));
                        if (t != null && t.isAlive && t.data.canPromote && t.data.pieceType != PieceType.C3 && !t.data.immuneToFlip && WorthFlipping(niko, t))
                            candidates.Add(t);
                    }
                if (candidates.Count == 0) return false;
                PieceInstance target = candidates[Random.Range(0, candidates.Count)];
                if (target.isPromoted) { target.Demote(); CombatResolver.PlayFlip(target); }
                else GameManager.Instance.PromotePiece(target);
                FloatingText.Spawn(target.boardPosition, "？", new Color(0.8f, 0.6f, 1f), 3.4f, 0.2f);
                return true;
            }
            case PieceType.Nako:
            case PieceType.Monin:
            {
                // 突撃・罵倒: 2マス以内の敵1体に2
                var targets = GetEnemiesInRange(niko.boardPosition, task.type == PieceType.Nako ? 2 : 1, foe, null);
                if (targets.Count == 0) return false;
                PieceInstance target = targets[Random.Range(0, targets.Count)];
                if (BattleEffects.Instance != null)
                {
                    if (task.type == PieceType.Nako) BattleEffects.Instance.PlayDashStreak(niko.boardPosition, target.boardPosition, NakoPink, 0.3f);
                    else BattleEffects.Instance.PlayStream(niko.boardPosition, target.boardPosition, new Color(1f, 0.35f, 0.3f));
                }
                CombatResolver.ApplyDamage(target, CombatResolver.AbilityDamage(niko, BalanceTuning.NikoStrikeDamage + CardBonus(niko)), false);
                return true;
            }
            case PieceType.Shimesaba:
            {
                // 正論パンチ: 隣の敵1体に2（防御無視）
                var targets = AdjacentEnemies(niko);
                if (targets.Count == 0) return false;
                PieceInstance target = targets[Random.Range(0, targets.Count)];
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayStream(niko.boardPosition, target.boardPosition, ArgueWhite);
                CombatResolver.ApplyDamage(target, CombatResolver.AbilityDamage(niko, BalanceTuning.NikoStrikeDamage + CardBonus(niko)), true);
                return true;
            }
            case PieceType.Monotetsu:
            {
                // 支援砲撃: 盤のどこかの敵1体に1（防御無視）
                var targets = new List<PieceInstance>();
                foreach (var e in bm.GetTeamPieces(foe))
                    if (e.isAlive && e.data.pieceType != PieceType.C3) targets.Add(e);
                if (targets.Count == 0) return false;
                PieceInstance target = targets[Random.Range(0, targets.Count)];
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayBombardmentEffect(niko.boardPosition, target.boardPosition);
                CombatResolver.ApplyDamage(target, CombatResolver.AbilityDamage(niko, 1 + CardBonus(niko)), true);
                return true;
            }
            case PieceType.Konishiki:
            case PieceType.Yuu:
            {
                // 小錦は防御、ユウは攻撃（ニコ自身。上限あり）
                if (SmallBuffOf(niko) >= BalanceTuning.NikoBuffCap + CardBonus(niko)) return false;
                bool atk = task.type == PieceType.Yuu;
                if (atk) niko.bonusATK += 1; else niko.bonusDEF += 1;
                smallBuffs[niko] = SmallBuffOf(niko) + 1;
                CombatResolver.RefreshStats(niko);
                FloatingText.Spawn(niko.boardPosition, atk ? "攻+1" : "防+1", atk ? Palette.ATK : Palette.DEF);
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayBuffEffect(niko.boardPosition, atk);
                return true;
            }
            case PieceType.SN:
            case PieceType.Kawasemi:
            {
                // 先代部長の教え・ジャンク修理: 周りの味方を1回復
                bool any = false;
                foreach (var a in AdjacentAllies(niko)) if (CombatResolver.Heal(a, 1 + CardBonus(niko)) > 0) any = true;
                return any;
            }
            case PieceType.Mitsuharu:
            {
                if (GameManager.Instance == null || GameManager.Instance.SecretFund >= BalanceTuning.MitsuharuFundMax) return false;
                int fund = 1 + CardBonus(niko);
                GameManager.Instance.SecretFund = Mathf.Min(BalanceTuning.MitsuharuFundMax, GameManager.Instance.SecretFund + fund);
                FloatingText.Spawn(niko.boardPosition, "裏金+" + fund, FundGold, 3f, 0.3f);
                return true;
            }
            default:
                return false;
        }
    }

    private int SmallBuffOf(PieceInstance p)
    {
        int n;
        return smallBuffs.TryGetValue(p, out n) ? n : 0;
    }

    /// <summary>周囲1マスの味方（C3・中華・ドパは除く）</summary>
    private static List<PieceInstance> AdjacentAllies(PieceInstance center)
    {
        BoardManager bm = BoardManager.Instance;
        var list = new List<PieceInstance>();
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                PieceInstance p = bm.GetPieceAt(new Vector2Int(center.boardPosition.x + dx, center.boardPosition.y + dy));
                if (p == null || !p.isAlive || p.team != center.team) continue;
                PieceType t = p.data.pieceType;
                if (t == PieceType.C3 || t == PieceType.Chuka || t == PieceType.Dopa) continue;
                list.Add(p);
            }
        return list;
    }

    // ============================================================
    // 〆鯖: レスバ（言い返す）と、成ると「世界が終わる」
    // ============================================================

    /// <summary>通常の攻撃を受けて生き残ったとき（CombatResolver.Attack から）</summary>
    public void OnAttackedAndSurvived(PieceInstance attacker, PieceInstance target)
    {
        if (target == null || attacker == null || !target.isAlive || !attacker.isAlive) return;
        if (target.data.pieceType != PieceType.Shimesaba || target.isSealed) return;
        int damage = CombatResolver.AbilityDamage(target, BalanceTuning.ShimesabaRetort + (target.awakened ? BalanceTuning.ShimesabaAwakenedRetortBonus : 0));
        FloatingText.Spawn(target.boardPosition, target.awakened ? "論破" : "レスバ", ArgueWhite, 3.2f);
        if (target.awakened && BattleEffects.Instance != null) BattleEffects.Instance.PlayAwakenBurst("LogicShatter", attacker.boardPosition, target.data.awakenColor);
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayStream(target.boardPosition, attacker.boardPosition, ArgueWhite);
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("〆鯖", target.team) + " が " + BattleLogUI.ColorName(attacker.DisplayName, attacker.team) + " に言い返した（" + damage + "ダメージ）");
        RunRoster.Feat(target);
        var source = GameSim.BeginSource(target);
        CombatResolver.ApplyDamage(attacker, damage, true);
        GameSim.EndSource(source);
    }

    /// <summary>〆鯖が成った: 世界が終わるまでの数え始め</summary>
    public void StartDoomsday(PieceInstance s)
    {
        doomCountdown[s] = DoomCountdown(s);
        FloatingText.Spawn(s.boardPosition, "あと" + DoomCountdown(s), ArgueWhite, 3.4f, 0.5f);
    }

    /// <summary>世界が終わるまでの手番（覚醒して成っていれば短い）</summary>
    private static int DoomCountdown(PieceInstance s)
    {
        return s.awakened && s.isPromoted ? BalanceTuning.ShimesabaAwakenedCountdown : BalanceTuning.ShimesabaCountdown;
    }

    private IEnumerator ExecuteDoomsday(Team team)
    {
        foreach (var s in AlivePieces(team, PieceType.Shimesaba))
        {
            if (!s.isAlive || !s.isPromoted || s.isSealed) continue;
            int left;
            if (!doomCountdown.TryGetValue(s, out left)) left = DoomCountdown(s);
            left--;
            if (left > 0)
            {
                doomCountdown[s] = left;
                FloatingText.Spawn(s.boardPosition, "あと" + left, ArgueWhite, 3.4f);
                continue;
            }
            doomCountdown[s] = DoomCountdown(s);

            // 世界が終わる: 盤上の敵すべてに（防御無視）
            if (!GameSim.Headless)
            {
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayCutInSound();
                yield return CutInUI.PlayPiece(s.data, true, "世界が終わる", "Hack分野の最終兵器", Palette.Hex(0x101014), Palette.Hex(0xE0E8F0), 1.1f);
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayWorldEnd(s.boardPosition);
                if (s.awakened && BattleEffects.Instance != null) BattleEffects.Instance.PlayAwakenBurst("LogicShatter", s.boardPosition, s.data.awakenColor);
            }
            if (BattleLogUI.Instance != null) BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("〆鯖", s.team) + " の最終兵器……世界が終わる");
            RunRoster.Feat(s);
            int damage = CombatResolver.AbilityDamage(s, BalanceTuning.ShimesabaEndDamage);
            var victims = new List<PieceInstance>();
            foreach (var e in BoardManager.Instance.GetTeamPieces(team == Team.Player ? Team.Enemy : Team.Player))
                if (e.isAlive && e.data.pieceType != PieceType.C3) victims.Add(e);
            var source = GameSim.BeginSource(s);
            foreach (var e in victims)
            {
                if (!e.isAlive) continue;
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayExplosionEffect(e.boardPosition);
                CombatResolver.ApplyDamage(e, damage, true);
            }
            GameSim.EndSource(source);
            yield return new WaitForSeconds(0.5f);
        }
    }

    // ============================================================
    // 翡翠: 最強PC（成ると、手番の終わりに何かが起きる）
    // ============================================================
    private static float KawasemiGoodChance(int parts)
    {
        return Mathf.Min(BalanceTuning.KawasemiGoodMax, BalanceTuning.KawasemiGoodBase + parts * BalanceTuning.KawasemiGoodPerPart);
    }

    private IEnumerator ExecuteKawasemiPC(Team team)
    {
        foreach (var k in AlivePieces(team, PieceType.Kawasemi))
        {
            if (!k.isAlive || !(k.isPromoted || k.awakened) || k.isSealed) continue;
            bool good = k.awakened || Random.value < KawasemiGoodChance(PartsOf(k));
            Team foe = team == Team.Player ? Team.Enemy : Team.Player;
            string what;
            if (good)
            {
                int roll = Random.Range(0, 3);
                if (roll == 0)
                {
                    // ベンチマーク: 周り2マスの敵すべてに2
                    what = "ベンチマーク";
                    var source = GameSim.BeginSource(k);
                    foreach (var e in GetEnemiesInRange(k.boardPosition, 2, foe, null))
                    {
                        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayStream(k.boardPosition, e.boardPosition, JadeGreen);
                        CombatResolver.ApplyDamage(e, CombatResolver.AbilityDamage(k, BalanceTuning.KawasemiBenchDamage), false);
                    }
                    GameSim.EndSource(source);
                }
                else if (roll == 1)
                {
                    // オーバークロック: 周り2マスの味方の攻撃+1（1体に上限あり）
                    what = "オーバークロック";
                    foreach (var a in GetEnemiesInRange(k.boardPosition, 2, team, null))
                    {
                        PieceType t = a.data.pieceType;
                        if (t == PieceType.Chuka || t == PieceType.Dopa || SmallBuffOf(a) >= BalanceTuning.NikoBuffCap) continue;
                        a.bonusATK += 1;
                        smallBuffs[a] = SmallBuffOf(a) + 1;
                        CombatResolver.RefreshStats(a);
                        FloatingText.Spawn(a.boardPosition, "攻+1", Palette.ATK);
                        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayBuffEffect(a.boardPosition, true);
                    }
                }
                else
                {
                    // 水冷: 周り2マスの味方を2回復
                    what = "水冷";
                    foreach (var a in GetEnemiesInRange(k.boardPosition, 2, team, null))
                        if (a.data.pieceType != PieceType.Chuka && a.data.pieceType != PieceType.Dopa) CombatResolver.Heal(a, BalanceTuning.KawasemiCoolHeal);
                }
                RunRoster.Feat(k);
            }
            else if (Random.value < 0.5f)
            {
                what = "爆熱";
                CombatResolver.ApplyDamage(k, 1, true);
            }
            else what = "再起動中…";

            FloatingText.Spawn(k.boardPosition, what, good ? JadeGreen : new Color(1f, 0.55f, 0.4f), 3.4f);
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlayPcEvent(k.boardPosition, good);
            if (k.awakened && BattleEffects.Instance != null) BattleEffects.Instance.PlayAwakenBurst("RgbFan", k.boardPosition, k.data.awakenColor);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("翡翠", team) + " の最強PC: " + what);
            yield return new WaitForSeconds(0.4f);
        }
    }

    /// <summary>誘うと来てくれる: 翡翠は、味方の部員の隣の空きマスへどこからでも動ける（1局に BalanceTuning.KawasemiInvites 回まで。負なら何度でも）</summary>
    public static void AddInviteMoves(PieceInstance k, List<MoveValidator.MoveResult> results)
    {
        if (k.data.pieceType != PieceType.Kawasemi || !CanInvite(k)) return;
        BoardManager bm = BoardManager.Instance;
        foreach (var p in bm.GetTeamPieces(k.team))
        {
            if (p == k || !p.isAlive || !IsMemberType(p.data.pieceType)) continue;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var pos = new Vector2Int(p.boardPosition.x + dx, p.boardPosition.y + dy);
                    if (!bm.IsInBounds(pos) || !bm.IsEmpty(pos) || pos == k.boardPosition) continue;
                    // 敵陣の中へは呼べない（成るには自分で歩いて入る）
                    if (!k.isPromoted && k.CanPromoteAt(pos.y, bm.CurrentBoardSize)) continue;
                    bool dup = false;
                    foreach (var r in results) if (r.position == pos) { dup = true; break; }
                    if (!dup) results.Add(new MoveValidator.MoveResult { position = pos, isAttack = false });
                }
        }
    }

    private static bool CanInvite(PieceInstance k)
    {
        if (BalanceTuning.KawasemiInvites < 0) return true;
        int used = 0;
        if (Instance != null) Instance.invitesUsed.TryGetValue(k, out used);
        return used < BalanceTuning.KawasemiInvites;
    }

    /// <summary>翡翠の動きが「誘うと来てくれる」だったら回数を数える（ふだんの動きで届くマスなら数えない）</summary>
    public void OnKawasemiMove(PieceInstance k, Vector2Int to)
    {
        if (k.data.pieceType != PieceType.Kawasemi || BalanceTuning.KawasemiInvites < 0) return;
        var normal = new List<MoveValidator.MoveResult>();
        MoveValidator.GetValidMoves(k, normal, false);
        foreach (var r in normal) if (r.position == to) return;
        int used;
        invitesUsed.TryGetValue(k, out used);
        invitesUsed[k] = used + 1;
        FloatingText.Spawn(k.boardPosition, "呼ばれて来た", JadeGreen, 3f);
    }

    /// <summary>新しい部員が覚醒した瞬間（光晴は世界一周へ出発、成っている〆鯖は世界が終わるまでが縮む）</summary>
    private void OnNewMemberAwakened(PieceInstance p)
    {
        switch (p.data.pieceType)
        {
            case PieceType.Mitsuharu:
                if (p.isAlive && !abroad.Contains(p))
                {
                    if (BattleEffects.Instance != null) BattleEffects.Instance.PlayAwakenBurst("WorldTrip", p.boardPosition, p.data.awakenColor, true);
                    SendAbroad(p);
                }
                break;
            case PieceType.Shimesaba:
            {
                // 成っていれば、世界が終わるまでの手番が縮む（成っていなければレスバが強くなるだけ）
                int left;
                if (p.isPromoted && (!doomCountdown.TryGetValue(p, out left) || left > DoomCountdown(p))) doomCountdown[p] = DoomCountdown(p);
                break;
            }
        }
    }

    /// <summary>部員（将棋の駒・C3・召喚物・艦娘・深海・敵専用の駒ではない）</summary>
    public static bool IsMemberType(PieceType t)
    {
        switch (t)
        {
            case PieceType.Nako: case PieceType.Monotetsu: case PieceType.Monin: case PieceType.Boku: case PieceType.Wotsu:
            case PieceType.Kei: case PieceType.Kipu: case PieceType.Rihaku: case PieceType.SN: case PieceType.Konishiki:
            case PieceType.Yuu: case PieceType.Mitsuharu: case PieceType.Niko: case PieceType.Shimesaba: case PieceType.Kawasemi:
                return true;
            default:
                return false;
        }
    }
}
