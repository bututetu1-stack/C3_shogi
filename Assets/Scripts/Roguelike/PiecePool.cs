using UnityEngine;
using System.Collections.Generic;

public static class PiecePool
{
    // レアリティ別の抽選重み
    private static readonly Dictionary<Rarity, float> rarityWeights = new Dictionary<Rarity, float>
    {
        { Rarity.Bronze, 60f },
        { Rarity.Normal, 50f },
        { Rarity.Rare, 30f },
        { Rarity.SuperRare, 15f },
        { Rarity.Legend, 5f }
    };

    public static List<PieceData> DrawPieces(PieceData[] allPieces, int count, List<PieceData> excludeList)
    {
        var result = new List<PieceData>();
        var available = new List<PieceData>();

        // 除外リストにない駒だけ候補に
        foreach (var piece in allPieces)
        {
            if (piece == null) continue;
            if (piece.pieceType == PieceType.C3) continue; // C3は抽選対象外
            if (piece.excludeFromDraft) continue; // 特殊駒は抽選対象外
            if (excludeList != null && excludeList.Contains(piece)) continue;
            available.Add(piece);
        }

        if (available.Count == 0) return result;

        // 重み付き抽選
        for (int i = 0; i < count && available.Count > 0; i++)
        {
            float totalWeight = 0f;
            foreach (var piece in available)
            {
                totalWeight += GetWeight(piece.rarity);
            }

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            PieceData selected = available[0];

            foreach (var piece in available)
            {
                cumulative += GetWeight(piece.rarity);
                if (roll <= cumulative)
                {
                    selected = piece;
                    break;
                }
            }

            result.Add(selected);
            available.Remove(selected);
        }

        return result;
    }

    private static float GetWeight(Rarity rarity)
    {
        float weight;
        if (rarityWeights.TryGetValue(rarity, out weight))
            return weight;
        return 10f;
    }
}
