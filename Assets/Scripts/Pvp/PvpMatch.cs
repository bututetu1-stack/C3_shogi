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
    /// <summary>後手の C3 の体力の上乗せ（先手が1手早いぶんの埋め合わせ）</summary>
    public const int SecondC3Bonus = 1;

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

    /// <summary>駒のコスト</summary>
    public static int CostOf(PieceData d)
    {
        if (d == null) return 0;
        switch (d.pieceType)
        {
            case PieceType.Pawn: return 1;
            case PieceType.Lance:
            case PieceType.Knight: return 2;
            case PieceType.Silver:
            case PieceType.Gold: return 3;
            case PieceType.Bishop:
            case PieceType.Rook: return 5;
            case PieceType.Kugutsu:
            case PieceType.Ootate: return 2;
            case PieceType.Yumihei:
            case PieceType.Dokuro: return 3;
            case PieceType.Kekkai: return 6;
        }
        switch (d.rarity)
        {
            case Rarity.Bronze: return 4;
            case Rarity.Rare: return 6;
            case Rarity.SuperRare:
            case Rarity.Legend: return 9;
            default: return 3;
        }
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
