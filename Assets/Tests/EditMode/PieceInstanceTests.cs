using NUnit.Framework;
using UnityEngine;

public class PieceInstanceTests
{
    private PieceData data;

    [SetUp]
    public void SetUp()
    {
        data = ScriptableObject.CreateInstance<PieceData>();
        data.displayName = "試駒";
        data.baseATK = 2;
        data.baseDEF = 1;
        data.baseHP = 3;
        data.canPromote = true;
        data.promotedATK = 3;
        data.promotedDEF = 2;
        data.promotedHP = 5;
        data.moveDirections = new[] { new MoveDirection(0, 1, 1) };
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(data);
    }

    [Test]
    public void AddMaxHP_RaisesBothMaxAndCurrent()
    {
        var piece = new PieceInstance(data, Team.Player, Vector2Int.zero);
        piece.AddMaxHP(2);
        Assert.AreEqual(5, piece.MaxHP);
        Assert.AreEqual(5, piece.currentHP);
    }

    [Test]
    public void Promote_AddsHpDifference_AndDemoteRemovesIt()
    {
        var piece = new PieceInstance(data, Team.Player, Vector2Int.zero);
        piece.Promote();
        Assert.IsTrue(piece.isPromoted);
        Assert.AreEqual(5, piece.currentHP);
        Assert.AreEqual(3, piece.ATK);

        piece.Demote();
        Assert.IsFalse(piece.isPromoted);
        Assert.AreEqual(3, piece.currentHP);
        Assert.AreEqual(2, piece.ATK);
    }

    [Test]
    public void Demote_NeverDropsHpBelowOne()
    {
        var piece = new PieceInstance(data, Team.Player, Vector2Int.zero);
        piece.Promote();
        piece.currentHP = 1;
        piece.Demote();
        Assert.AreEqual(1, piece.currentHP);
    }

    [Test]
    public void EnemyMoveDirections_AreFlippedVertically()
    {
        var player = new PieceInstance(data, Team.Player, Vector2Int.zero);
        var enemy = new PieceInstance(data, Team.Enemy, Vector2Int.zero);
        Assert.AreEqual(new Vector2Int(0, 1), player.GetMoveDirections()[0].direction);
        Assert.AreEqual(new Vector2Int(0, -1), enemy.GetMoveDirections()[0].direction);
    }

    [Test]
    public void CanPromoteAt_UsesFarRowsForEachTeam()
    {
        var player = new PieceInstance(data, Team.Player, Vector2Int.zero);
        var enemy = new PieceInstance(data, Team.Enemy, Vector2Int.zero);
        Assert.IsTrue(player.CanPromoteAt(8, 9));
        Assert.IsTrue(player.CanPromoteAt(6, 9));
        Assert.IsFalse(player.CanPromoteAt(5, 9));
        Assert.IsTrue(enemy.CanPromoteAt(0, 5));
        Assert.IsTrue(enemy.CanPromoteAt(1, 5));
        Assert.IsFalse(enemy.CanPromoteAt(2, 5));
    }

    [Test]
    public void CalcDamage_IsAttackMinusDefense_NeverNegative()
    {
        var attacker = new PieceInstance(data, Team.Player, Vector2Int.zero);
        var target = new PieceInstance(data, Team.Enemy, Vector2Int.one);
        Assert.AreEqual(1, CombatResolver.CalcDamage(attacker, target));
        target.bonusDEF = 5;
        Assert.AreEqual(0, CombatResolver.CalcDamage(attacker, target));
    }
}
