using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class PiecePoolTests
{
    private readonly List<Object> created = new List<Object>();

    private PieceData Make(PieceType type, Rarity rarity, bool exclude = false)
    {
        var d = ScriptableObject.CreateInstance<PieceData>();
        d.pieceType = type;
        d.rarity = rarity;
        d.excludeFromDraft = exclude;
        d.displayName = type.ToString();
        d.pieceName = type.ToString();
        created.Add(d);
        return d;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in created) Object.DestroyImmediate(o);
        created.Clear();
    }

    [Test]
    public void OwnedUniquePiece_IsNeverOffered()
    {
        var monin = Make(PieceType.Monin, Rarity.Rare);
        var boku = Make(PieceType.Boku, Rarity.Rare);
        var owned = new List<PieceData> { monin };
        var all = new[] { Make(PieceType.C3, Rarity.Normal), Make(PieceType.Pawn, Rarity.Normal), monin, boku };

        for (int i = 0; i < 50; i++)
        {
            foreach (var option in PiecePool.DrawOptions(all, 3, owned, 1, true))
            {
                Assert.AreNotEqual(monin, option.piece, "仲間にした部員がまた出てきた");
                Assert.IsTrue(option.piece == null || (option.piece.pieceType != PieceType.C3 && option.piece.pieceType != PieceType.Pawn));
            }
        }
    }

    [Test]
    public void StandardPiece_CanBeOfferedAgain()
    {
        var gold = Make(PieceType.Gold, Rarity.Normal);
        var owned = new List<PieceData> { gold };
        var options = PiecePool.DrawOptions(new[] { gold }, 3, owned, 1, true);
        Assert.IsTrue(options.Exists(o => o.piece == gold), "素の将棋駒は何枚でも仲間にできる");
    }

    [Test]
    public void NoDeploySlot_OffersOnlyDistinctUpgrades()
    {
        var all = new[] { Make(PieceType.Gold, Rarity.Normal), Make(PieceType.Boku, Rarity.Rare) };
        var options = PiecePool.DrawOptions(all, 3, new List<PieceData>(), 5, false);
        Assert.AreEqual(3, options.Count);
        var kinds = new HashSet<UpgradeKind>();
        foreach (var o in options)
        {
            Assert.IsTrue(o.IsUpgrade);
            Assert.IsTrue(kinds.Add(o.upgrade), "同じ強化が並んだ");
        }
    }

    [Test]
    public void EmptyPool_IsFilledWithUpgrades()
    {
        var options = PiecePool.DrawOptions(new PieceData[0], 3, new List<PieceData>(), 1, true);
        Assert.AreEqual(3, options.Count);
        Assert.IsTrue(options.TrueForAll(o => o.IsUpgrade));
    }

    [Test]
    public void ExcludedPieces_AreNeverOffered()
    {
        var chuka = Make(PieceType.Chuka, Rarity.Normal, true);
        for (int i = 0; i < 20; i++)
            foreach (var o in PiecePool.DrawOptions(new[] { chuka }, 3, new List<PieceData>(), 1, true))
                Assert.AreNotEqual(chuka, o.piece);
    }
}
