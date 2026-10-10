using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class AwakeningTests
{
    private readonly List<Object> created = new List<Object>();

    private PieceData Make(PieceType type, Rarity rarity, bool canAwaken = true)
    {
        var d = ScriptableObject.CreateInstance<PieceData>();
        d.pieceType = type;
        d.rarity = rarity;
        d.displayName = type.ToString();
        d.pieceName = type.ToString();
        d.baseATK = 1; d.baseDEF = 1; d.baseHP = 3;
        d.canAwaken = canAwaken;
        d.awakenedName = "覚醒" + type;
        d.awakenedATK = 1; d.awakenedDEF = 2; d.awakenedHP = 4;
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
    public void Apply_AddsStatsOnce()
    {
        var p = new PieceInstance(Make(PieceType.Konishiki, Rarity.Rare), Team.Player, Vector2Int.zero);
        Assert.IsTrue(Awakening.Apply(p));
        Assert.IsTrue(p.awakened);
        Assert.AreEqual(2, p.ATK);
        Assert.AreEqual(3, p.DEF);
        Assert.AreEqual(7, p.MaxHP);
        Assert.AreEqual(7, p.currentHP);
        Assert.AreEqual("覚醒Konishiki", p.FullName);
        Assert.IsFalse(Awakening.Apply(p), "二度は覚醒しない");
        Assert.AreEqual(2, p.ATK);
    }

    [Test]
    public void Apply_IgnoresPiecesThatCannotAwaken()
    {
        var p = new PieceInstance(Make(PieceType.SN, Rarity.SuperRare, false), Team.Player, Vector2Int.zero);
        Assert.IsFalse(Awakening.Apply(p));
        Assert.IsFalse(p.awakened);
    }

    [Test]
    public void ThreeStarMember_AwakensAfterEnoughActivities()
    {
        var roster = new RunRoster();
        PieceData data = Make(PieceType.Boku, Rarity.Rare);
        RunMember m = roster.Add(data);
        roster.AddXp(m, 100);   // ★3
        var p = new PieceInstance(data, Team.Player, Vector2Int.zero);
        for (int i = 0; i < BalanceTuning.AwakenActivities - 1; i++) roster.OnFeat(p);
        Assert.IsFalse(p.awakened, "活躍が足りないうちは覚醒しない");
        roster.OnFeat(p);
        Assert.IsTrue(p.awakened, "★3の部員は、その局の活躍を重ねると覚醒する");
    }

    [Test]
    public void BelowThreeStars_NeverAwakens()
    {
        var roster = new RunRoster();
        PieceData data = Make(PieceType.Kei, Rarity.Bronze);
        roster.Add(data);
        var p = new PieceInstance(data, Team.Player, Vector2Int.zero);
        for (int i = 0; i < 10; i++) roster.OnFeat(p);
        // 活躍の練度は1局に上限があるので、★3には届かない
        Assert.Less(roster.Get(PieceType.Kei).stars, 3);
        Assert.IsFalse(p.awakened);
    }

    [Test]
    public void PushDestination_IsOneStepAwayFromPusher()
    {
        Assert.AreEqual(new Vector2Int(3, 4), Awakening.PushDestination(new Vector2Int(1, 2), new Vector2Int(2, 3)));
        Assert.AreEqual(new Vector2Int(4, 2), Awakening.PushDestination(new Vector2Int(2, 2), new Vector2Int(3, 2)));
        Assert.AreEqual(new Vector2Int(2, 0), Awakening.PushDestination(new Vector2Int(2, 2), new Vector2Int(2, 1)));
    }

    [Test]
    public void AwakenedMoves_OverrideNormalMoves()
    {
        PieceData data = Make(PieceType.Yuu, Rarity.SuperRare);
        data.moveDirections = new[] { new MoveDirection(0, 1, 1) };
        data.awakenedMoveDirections = new[] { new MoveDirection(1, 0, 2), new MoveDirection(-1, 0, 2) };
        var p = new PieceInstance(data, Team.Player, Vector2Int.zero);
        Assert.AreEqual(1, p.GetMoveDirections().Length);
        Awakening.Apply(p);
        Assert.AreEqual(2, p.GetMoveDirections().Length);
    }
}
