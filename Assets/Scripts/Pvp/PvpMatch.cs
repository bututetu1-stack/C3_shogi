using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 対人戦（2026-10-12〜）の対局の状態と決まり。
/// 先手は Team.Player（盤の下）、後手は Team.Enemy（盤の上）。いまは1台で2人が交代で指す形（オンラインは次の段で足す）。
/// 練度・覚醒・局の上乗せは使わない。編成は「コスト制」と「ランダム配布」から選ぶ。
/// </summary>
public static class PvpMatch
{
    public enum Rule { Cost, Random }

    /// <summary>片方の陣営の編成</summary>
    public class Side
    {
        public Team team;
        public readonly List<PieceData> pieces = new List<PieceData>();
        public readonly List<UpgradeKind> upgrades = new List<UpgradeKind>();
        /// <summary>光春の裏金（対局のあいだ）</summary>
        public int secretFund;

        public int CostUsed()
        {
            int total = 0;
            foreach (var p in pieces) total += CostOf(p);
            total += upgrades.Count * UpgradeCost;
            return total;
        }
    }

    /// <summary>対人戦の最中か（タイトルで選んでから、決着してタイトルに戻るまで）</summary>
    public static bool Active;
    public static Rule CurrentRule = Rule.Cost;
    public static Side First = new Side { team = Team.Player };
    public static Side Second = new Side { team = Team.Enemy };
    /// <summary>この対局の乱数の種（巨岩の置き場所・ランダム配布の候補など。オンラインでは2人で同じ種を使う）</summary>
    public static int Seed;
    /// <summary>オンラインの対人戦か（false なら1台で2人）</summary>
    public static bool Online;
    /// <summary>オンラインで、この画面の人が動かす陣営</summary>
    public static Team LocalTeam = Team.Player;

    /// <summary>決着画面の「もう一度」で、同じルールの対人戦から始め直す（場面を読み直しても残る）</summary>
    public static bool RestartPending;

    // ---- 決まり（数値はAIどうしの対局で直す叩き台） ----
    public const int Budget = 30;
    public const int UpgradeCost = 5;
    public const int MaxUpgrades = 2;
    public const int RandomRounds = 6;
    /// <summary>ランダム配布の候補のレアの出方（1人用の第七局と同じ）</summary>
    public const int RandomDraftStage = 7;
    public const int BoardSize = 9;
    /// <summary>後手の C3 の体力の上乗せ（BalanceTuning.PvpSecondC3Bonus）</summary>
    public static int SecondC3Bonus { get { return BalanceTuning.PvpSecondC3Bonus; } }

    /// <summary>コスト制で選べる部員（仲間の候補に出る18人）</summary>
    public static readonly PieceType[] Members =
    {
        PieceType.Monotetsu, PieceType.Kei, PieceType.Mitsuharu, PieceType.Konishiki, PieceType.Mutou, PieceType.Shimesaba,
        PieceType.Boku, PieceType.Monin, PieceType.Nako, PieceType.Wotsu, PieceType.Kipu, PieceType.Negu, PieceType.RN,
        PieceType.Rihaku, PieceType.SN, PieceType.Kawasemi, PieceType.Niko, PieceType.Yuu,
    };
    /// <summary>素の将棋駒（何枚でも）</summary>
    public static readonly PieceType[] StandardPieces =
    {
        PieceType.Pawn, PieceType.Lance, PieceType.Knight, PieceType.Silver, PieceType.Gold, PieceType.Bishop, PieceType.Rook,
    };
    /// <summary>敵専用の駒で、対人戦の編成で選べるもの（結界は1枚まで）</summary>
    public static readonly PieceType[] EnemyPieces =
    {
        PieceType.Kugutsu, PieceType.Ootate, PieceType.Yumihei, PieceType.Dokuro, PieceType.Kekkai,
    };
    /// <summary>対人戦で使える全軍強化（艦隊・歩兵の強化は使わない）</summary>
    public static readonly UpgradeKind[] Upgrades =
    {
        UpgradeKind.AllATK, UpgradeKind.AllDEF, UpgradeKind.AllHP, UpgradeKind.C3HP,
    };

    public static Side SideOf(Team team) { return team == Team.Player ? First : Second; }
    public static string SideName(Team team) { return team == Team.Player ? "先手" : "後手"; }
    public static string SideLong(Team team) { return team == Team.Player ? "先手（下）" : "後手（上）"; }

    /// <summary>部員のコストの幅（ルールの説明に出す。盤の駒のデータがまだなければ 0〜0）</summary>
    public static void MemberCostRange(out int lo, out int hi)
    {
        lo = int.MaxValue; hi = 0;
        foreach (var t in Members)
        {
            PieceData d = BoardManager.Instance != null ? BoardManager.Instance.GetPieceDataByType(t) : null;
            if (d == null) continue;
            int c = CostOf(d);
            lo = Mathf.Min(lo, c);
            hi = Mathf.Max(hi, c);
        }
        if (hi == 0) lo = 0;
    }

    /// <summary>駒のコスト</summary>
    public static int CostOf(PieceData d)
    {
        if (d == null) return 0;
        int over;
        if (Overrides().TryGetValue(d.pieceType, out over)) return over;
        int cost;
        if (CostTable.TryGetValue(d.pieceType, out cost)) return cost;
        switch (d.rarity)
        {
            case Rarity.Bronze: return 4;
            case Rarity.Rare: return 6;
            case Rarity.SuperRare:
            case Rarity.Legend: return 9;
            default: return 3;
        }
    }

    /// <summary>
    /// コスト表（ここにない部員はレアリティどおり: ブロンズ4・レア6・激レア9）。
    /// 2026-10-12、AIどうしの対人戦600局ずつで、入っていた側の勝ち点が50%に近づくように決めた叩き台（docs/balance/pvp_stats.js）。
    /// 僕・光春は対人戦だけの上限（PvpBokuBuffCap・PvpSouvenirCap）を入れたあとの値。
    /// 運営・鯖缶を入れたあと、僕はコスト制で勝ち点40%に下がったので10→9（54%）
    /// </summary>
    private static readonly Dictionary<PieceType, int> CostTable = new Dictionary<PieceType, int>
    {
        // 部員（強い・弱いをレアリティの値から動かした）
        { PieceType.Boku, 9 }, { PieceType.Mitsuharu, 6 }, { PieceType.Shimesaba, 8 }, { PieceType.Konishiki, 7 },
        { PieceType.Nako, 7 }, { PieceType.Kei, 5 }, { PieceType.Kipu, 5 }, { PieceType.RN, 5 }, { PieceType.Negu, 4 },
        { PieceType.Rihaku, 5 }, { PieceType.SN, 6 }, { PieceType.Niko, 8 }, { PieceType.Kawasemi, 8 },
        // 将棋の駒
        { PieceType.Pawn, 1 }, { PieceType.Lance, 2 }, { PieceType.Knight, 2 }, { PieceType.Silver, 2 }, { PieceType.Gold, 2 },
        { PieceType.Bishop, 4 }, { PieceType.Rook, 5 },
        // 敵の駒
        { PieceType.Kugutsu, 2 }, { PieceType.Ootate, 2 }, { PieceType.Yumihei, 4 }, { PieceType.Dokuro, 3 }, { PieceType.Kekkai, 3 },
    };

    private static string parsedOverrides;
    private static readonly Dictionary<PieceType, int> overrides = new Dictionary<PieceType, int>();

    /// <summary>BalanceTuning.PvpCostOverrides（"Boku:8,Mitsuharu:6"）を読む</summary>
    private static Dictionary<PieceType, int> Overrides()
    {
        string text = BalanceTuning.PvpCostOverrides ?? "";
        if (text == parsedOverrides) return overrides;
        parsedOverrides = text;
        overrides.Clear();
        foreach (string item in text.Split(','))
        {
            string[] kv = item.Split(':');
            PieceType t;
            int cost;
            if (kv.Length == 2 && System.Enum.TryParse(kv[0].Trim(), out t) && int.TryParse(kv[1].Trim(), out cost)) overrides[t] = cost;
        }
        return overrides;
    }

    /// <summary>
    /// 人が手で動かせる駒か。1人用は isManualControllable どおり（敵専用の駒は AI が動かす）。
    /// 対人戦は敵専用の駒（髑髏など）も持ち主が動かせる（自動で動く駒・動けない駒は別）
    /// </summary>
    public static bool CanMoveByHand(PieceData d)
    {
        if (d == null) return false;
        if (d.isManualControllable) return true;
        return Active && !d.isAutoMove && !d.isImmovable;
    }

    /// <summary>同じ陣営に1枚までの駒（部員と結界）</summary>
    public static bool IsUnique(PieceType t)
    {
        return System.Array.IndexOf(Members, t) >= 0 || t == PieceType.Kekkai;
    }

    /// <summary>自陣に置ける駒の数（C3を除く手前3段）</summary>
    public static int MaxPieces { get { return BoardSize * 3 - 1; } }

    /// <summary>この駒を足せるか（できなければ理由を返す。できれば null）</summary>
    public static string CannotAdd(Side side, PieceData d)
    {
        if (d == null) return "駒がない";
        if (IsUnique(d.pieceType) && side.pieces.Exists(p => p.pieceType == d.pieceType)) return "1枚まで";
        if (side.pieces.Count >= MaxPieces) return "自陣がいっぱい";
        if (CurrentRule == Rule.Cost && side.CostUsed() + CostOf(d) > Budget) return "持ち点が足りない";
        return null;
    }

    public static string CannotAddUpgrade(Side side)
    {
        if (side.upgrades.Count >= MaxUpgrades) return MaxUpgrades + "枚まで";
        if (CurrentRule == Rule.Cost && side.CostUsed() + UpgradeCost > Budget) return "持ち点が足りない";
        return null;
    }

    /// <summary>新しい対局を始める（編成を空に、種を決める）</summary>
    public static void Begin(Rule rule)
    {
        Active = true;
        Online = false;
        CurrentRule = rule;
        First = new Side { team = Team.Player };
        Second = new Side { team = Team.Enemy };
        Seed = System.Environment.TickCount & 0x7fffffff;
        GameRng.InitState(Seed);
    }

    /// <summary>オンラインの対局を始める（種と先手は部屋を作った人が決めて、2人で同じものを使う）</summary>
    public static void BeginOnline(Rule rule, int seed, Team localTeam)
    {
        Active = true;
        Online = true;
        LocalTeam = localTeam;
        CurrentRule = rule;
        First = new Side { team = Team.Player };
        Second = new Side { team = Team.Enemy };
        Seed = seed;
        GameRng.InitState(Seed);
    }

    public static void End()
    {
        Active = false;
        Online = false;
    }

    /// <summary>この画面の人の陣営の呼び方（オンラインは「あなた／相手」）</summary>
    public static string WhoName(Team team)
    {
        if (!Online) return SideName(team);
        return team == LocalTeam ? "あなた" : "相手";
    }

    /// <summary>
    /// 自陣に駒を並べる順（C3 のある手前の段の真ん中を除く）。
    /// 先手は手前2段目の真ん中から外へ、次に3段目（前線）、最後に1段目。後手は盤の中心で点対称にした位置。
    /// </summary>
    public static List<Vector2Int> PlacementOrder(Team team)
    {
        var order = new List<Vector2Int>();
        int c = BoardSize / 2;
        foreach (int y in new[] { 1, 2, 0 })
        {
            order.Add(new Vector2Int(c, y));
            for (int d = 1; d <= c; d++)
            {
                order.Add(new Vector2Int(c - d, y));
                order.Add(new Vector2Int(c + d, y));
            }
        }
        order.Remove(new Vector2Int(c, 0));   // C3
        if (team == Team.Enemy)
            for (int i = 0; i < order.Count; i++) order[i] = new Vector2Int(BoardSize - 1 - order[i].x, BoardSize - 1 - order[i].y);
        return order;
    }

    /// <summary>ランダム配布の候補（RandomRounds 回ぶん。同じ部員は2回出ない。2人に同じものを出す。GameRng を使う）</summary>
    public static List<List<DraftOption>> BuildRandomRounds()
    {
        var rounds = new List<List<DraftOption>>();
        var offered = new List<PieceData>();
        PieceData[] all = BoardManager.Instance != null ? BoardManager.Instance.allPieceData : new PieceData[0];
        for (int r = 0; r < RandomRounds; r++)
        {
            var options = PiecePool.DrawOptions(all, 3, offered, RandomDraftStage, true, null);
            // 対人戦で使わない強化（応急修理・歩兵を消す）は、使える強化に差し替える
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].piece != null) { offered.Add(options[i].piece); continue; }
                if (options[i].train != null || System.Array.IndexOf(Upgrades, options[i].upgrade) < 0)
                    options[i] = DraftOption.Upgrade(Upgrades[GameRng.Range(0, Upgrades.Length)]);
            }
            rounds.Add(options);
        }
        return rounds;
    }

    /// <summary>コスト制の編成をランダムに作る（自動プレイ用。持ち点に収まる札を、選べなくなるまで1枚ずつ足す）</summary>
    public static void RandomCostBuild(Side side)
    {
        BoardManager bm = BoardManager.Instance;
        for (int guard = 0; guard < 100; guard++)
        {
            var pieces = new List<PieceData>();
            foreach (var group in new[] { Members, StandardPieces, EnemyPieces })
                foreach (var t in group)
                {
                    PieceData d = bm.GetPieceDataByType(t);
                    if (d != null && CannotAdd(side, d) == null) pieces.Add(d);
                }
            bool upgradeOk = CannotAddUpgrade(side) == null;
            int total = pieces.Count + (upgradeOk ? Upgrades.Length : 0);
            if (total == 0) return;
            int pick = GameRng.Range(0, total);
            if (pick < pieces.Count) side.pieces.Add(pieces[pick]);
            else side.upgrades.Add(Upgrades[pick - pieces.Count]);
        }
    }

    /// <summary>巨岩の置き場所（盤の中心で点対称に2〜4個。中段の3段のどこか）</summary>
    public static List<Vector2Int> RockSpots()
    {
        var spots = new List<Vector2Int>();
        int pairs = GameRng.Range(1, 3);
        int guard = 0;
        while (spots.Count < pairs * 2 && guard++ < 50)
        {
            var a = new Vector2Int(GameRng.Range(0, BoardSize), GameRng.Range(3, 5));
            var b = new Vector2Int(BoardSize - 1 - a.x, BoardSize - 1 - a.y);
            if (a == b || spots.Contains(a) || spots.Contains(b)) continue;
            spots.Add(a);
            spots.Add(b);
        }
        return spots;
    }
}
