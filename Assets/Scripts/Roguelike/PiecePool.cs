using UnityEngine;
using System.Collections.Generic;

/// <summary>ステージ前の選択肢の種類（仲間の駒、または全軍の強化）</summary>
public enum UpgradeKind
{
    None,
    AllATK,   // C3以外の全員の攻撃+1
    AllDEF,   // C3以外の全員の防御+1
    AllHP,    // C3以外の全員の体力+2
    C3HP,     // C3の体力+4
    DamageControl  // 応急修理要員（艦娘が沈むとき1回だけ全快。物鉄を仲間にしているときだけ出る）
}

/// <summary>仲間選択の1枚のカード</summary>
public class DraftOption
{
    public PieceData piece;
    public UpgradeKind upgrade;
    /// <summary>提督の艦隊のS勝利で増えた1枚</summary>
    public bool fleetReward;

    public bool IsUpgrade { get { return piece == null; } }

    public static DraftOption Piece(PieceData data) { return new DraftOption { piece = data }; }
    public static DraftOption Upgrade(UpgradeKind kind) { return new DraftOption { upgrade = kind }; }

    public string Title
    {
        get
        {
            if (piece != null) return piece.pieceName;
            switch (upgrade)
            {
                case UpgradeKind.AllATK: return "士気高揚";
                case UpgradeKind.AllDEF: return "鉄壁の構え";
                case UpgradeKind.AllHP: return "部室の差し入れ";
                case UpgradeKind.C3HP: return "部の結束";
                case UpgradeKind.DamageControl: return "応急修理要員";
                default: return "";
            }
        }
    }

    public string Description
    {
        get
        {
            if (piece != null) return piece.description;
            switch (upgrade)
            {
                case UpgradeKind.AllATK: return "C3以外の味方全員の攻撃力が +1 される。この効果は最後まで続く。";
                case UpgradeKind.AllDEF: return "C3以外の味方全員の防御力が +1 される。この効果は最後まで続く。";
                case UpgradeKind.AllHP: return "C3以外の味方全員の体力が +2 される。この効果は最後まで続く。";
                case UpgradeKind.C3HP: return "C3の体力が +4 される。この効果は最後まで続く。";
                case UpgradeKind.DamageControl: return "提督の艦娘が沈むとき、1回だけ体力が全快して踏みとどまる。使うまで最後まで残る。";
                default: return "";
            }
        }
    }

    /// <summary>強化カードのアイコンの文字と色</summary>
    public string Glyph
    {
        get
        {
            switch (upgrade)
            {
                case UpgradeKind.AllATK: return "攻";
                case UpgradeKind.AllDEF: return "防";
                case UpgradeKind.AllHP: return "体";
                case UpgradeKind.C3HP: return "C3";
                case UpgradeKind.DamageControl: return "修";
                default: return "";
            }
        }
    }

    public Color GlyphColor
    {
        get
        {
            switch (upgrade)
            {
                case UpgradeKind.AllATK: return Palette.ATK;
                case UpgradeKind.AllDEF: return Palette.DEF;
                case UpgradeKind.AllHP: return Palette.HP;
                case UpgradeKind.DamageControl: return CutInUI.SeaLight;
                default: return Palette.Gold;
            }
        }
    }
}

public static class PiecePool
{
    // レアリティ別の抽選重み（ステージが進むほど高レアが出やすくなる）
    private static float GetWeight(Rarity rarity, int stage)
    {
        int s = Mathf.Max(0, stage - 1);
        switch (rarity)
        {
            // 序盤はブロンズ中心、局が進むほどレア・激レアが出やすくなる。
            // （部員だけのとき、第一局の1枠あたり: ブロンズ約43% / レア約43% / 激レア約13%。部員の数で変わる）
            // ノーマルは素の将棋駒で、いまは仲間の候補に出ない（BalanceTuning.DraftStandardPieces）
            case Rarity.Normal: return Mathf.Max(4f, 10f * (1f - 0.04f * s));
            case Rarity.Bronze: return Mathf.Max(12f, 30f * (1f - 0.035f * s));
            case Rarity.Rare: return 12f * (1f + 0.12f * s);
            case Rarity.SuperRare: return 4.5f * (1f + 0.3f * s);
            case Rarity.Legend: return 2f * (1f + 0.3f * s);
            default: return 10f;
        }
    }

    /// <summary>素の将棋駒（香・桂・銀・金・角・飛）。仲間の候補に出すときは何枚でも仲間にできる</summary>
    public static bool IsStandardPiece(PieceData data)
    {
        return data.pieceType >= PieceType.Lance && data.pieceType <= PieceType.Rook;
    }

    /// <summary>
    /// 仲間にした素の将棋駒は最初からいる歩兵より鍛えられている: 体力の上乗せ（BalanceTuning）。
    /// 敵の同じ駒や、最初からいる歩兵には付かない
    /// </summary>
    public static int RecruitBonusHP(PieceData data)
    {
        return data != null && IsStandardPiece(data) ? BalanceTuning.RecruitBonusHP : 0;
    }

    /// <summary>仲間にした飛車・角は攻撃にも上乗せ（BalanceTuning）</summary>
    public static int RecruitBonusATK(PieceData data)
    {
        return data != null && (data.pieceType == PieceType.Bishop || data.pieceType == PieceType.Rook) ? BalanceTuning.RecruitBonusATK : 0;
    }

    /// <summary>
    /// 仲間選択のカードを引く。
    /// owned: すでに仲間にしている駒（固有の部員は重複しない）。canDeploy: 駒を置ける空きがあるか
    /// </summary>
    public static List<DraftOption> DrawOptions(PieceData[] allPieces, int count, List<PieceData> owned, int stage, bool canDeploy)
    {
        var result = new List<DraftOption>();

        // 強化カード: ステージ3以降はときどき1枚混ざる
        int upgradeSlots = 0;
        if (stage >= 3 && Random.value < 0.45f) upgradeSlots = 1;
        if (!canDeploy) upgradeSlots = count;

        var available = new List<PieceData>();
        if (canDeploy)
        {
            foreach (var piece in allPieces)
            {
                if (piece == null) continue;
                if (piece.pieceType == PieceType.C3 || piece.pieceType == PieceType.Pawn) continue;
                if (piece.excludeFromDraft) continue;
                // 仲間の候補は部員だけ（素の将棋駒は BalanceTuning.DraftStandardPieces のときだけ）
                if (IsStandardPiece(piece) && !BalanceTuning.DraftStandardPieces) continue;
                if (!IsStandardPiece(piece) && owned != null && owned.Contains(piece)) continue;
                available.Add(piece);
            }
        }

        int pieceSlots = Mathf.Min(count - upgradeSlots, available.Count);
        for (int i = 0; i < pieceSlots; i++)
        {
            float total = 0f;
            foreach (var p in available) total += GetWeight(p.rarity, stage);
            float roll = Random.Range(0f, total);
            PieceData selected = available[available.Count - 1];
            float cumulative = 0f;
            foreach (var p in available)
            {
                cumulative += GetWeight(p.rarity, stage);
                if (roll <= cumulative) { selected = p; break; }
            }
            result.Add(DraftOption.Piece(selected));
            available.Remove(selected);
        }

        // 足りない分は強化カードで埋める（同じ強化は並ばない）
        var upgrades = UpgradeKinds(owned);
        while (result.Count < count && upgrades.Count > 0)
        {
            int idx = Random.Range(0, upgrades.Count);
            result.Add(DraftOption.Upgrade(upgrades[idx]));
            upgrades.RemoveAt(idx);
        }
        return result;
    }

    /// <summary>出せる強化カード（応急修理要員は物鉄を仲間にしているときだけ）</summary>
    private static List<UpgradeKind> UpgradeKinds(List<PieceData> owned)
    {
        var kinds = new List<UpgradeKind> { UpgradeKind.AllATK, UpgradeKind.AllDEF, UpgradeKind.AllHP, UpgradeKind.C3HP };
        if (owned != null && owned.Exists(p => p != null && p.pieceType == PieceType.Monotetsu))
            kinds.Add(UpgradeKind.DamageControl);
        return kinds;
    }

    /// <summary>選択肢に、まだ並んでいない強化カードを1枚足す（艦隊のS勝利のごほうび）</summary>
    public static void AddExtraUpgrade(List<DraftOption> options, List<PieceData> owned)
    {
        var kinds = UpgradeKinds(owned);
        kinds.RemoveAll(k => options.Exists(o => o.IsUpgrade && o.upgrade == k));
        if (kinds.Count == 0) return;
        DraftOption reward = DraftOption.Upgrade(kinds[Random.Range(0, kinds.Count)]);
        reward.fleetReward = true;
        options.Add(reward);
    }
}
