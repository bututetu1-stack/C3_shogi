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

    [Test]
    public void AbilityDamage_AddsAttackBonus_AndIsAtLeastOne()
    {
        var piece = new PieceInstance(data, Team.Player, Vector2Int.zero);
        Assert.AreEqual(2, CombatResolver.AbilityDamage(piece, 2));
        piece.bonusATK = 3;
        Assert.AreEqual(5, CombatResolver.AbilityDamage(piece, 2));
        piece.bonusATK = -2;
        Assert.AreEqual(1, CombatResolver.AbilityDamage(piece, 2));
    }

    [Test]
    public void YuuRunUp_AddsAttackOnlyFromTwoSquaresAway()
    {
        var yuuData = ScriptableObject.CreateInstance<PieceData>();
        yuuData.pieceType = PieceType.Yuu;
        yuuData.baseATK = 2;
        yuuData.baseHP = 4;
        yuuData.canPromote = true;
        yuuData.promotedATK = 2;
        yuuData.promotedHP = 4;
        try
        {
            var yuu = new PieceInstance(yuuData, Team.Player, new Vector2Int(0, 0));
            var near = new PieceInstance(data, Team.Enemy, new Vector2Int(1, 1));   // 防御1
            var far = new PieceInstance(data, Team.Enemy, new Vector2Int(0, 3));
            Assert.AreEqual(1, CombatResolver.CalcDamage(yuu, near), "隣は助走なし");
            Assert.AreEqual(2, CombatResolver.CalcDamage(yuu, far), "2マス以上走ると攻撃+1");
            yuu.Promote();
            Assert.AreEqual(3, CombatResolver.CalcDamage(yuu, far), "成ると攻撃+2");
        }
        finally
        {
            Object.DestroyImmediate(yuuData);
        }
    }

    [Test]
    public void AbilityDamage_CanLeaveOutStageBonus()
    {
        var piece = new PieceInstance(data, Team.Enemy, Vector2Int.zero);
        piece.stageBonusATK = 3;
        piece.bonusATK = 4;   // 局による強化3 + 軍将1
        Assert.AreEqual(5, CombatResolver.AbilityDamage(piece, 1));
        Assert.AreEqual(2, CombatResolver.AbilityDamage(piece, 1, false));
    }

    [Test]
    public void Lower_AttackAndDefenseStopAtZero()
    {
        var piece = new PieceInstance(data, Team.Enemy, Vector2Int.zero);
        piece.Lower(StatKind.DEF);
        Assert.AreEqual(0, piece.DEF);
        Assert.IsFalse(piece.CanLower(StatKind.DEF));
        piece.Lower(StatKind.DEF);
        Assert.AreEqual(0, piece.DEF);
        piece.Lower(StatKind.ATK);
        Assert.AreEqual(1, piece.ATK);
    }

    [Test]
    public void Lower_HpLowersMaxAndCurrent_ButNeverToZero()
    {
        var piece = new PieceInstance(data, Team.Enemy, Vector2Int.zero);
        piece.Lower(StatKind.HP);
        Assert.AreEqual(2, piece.currentHP);
        Assert.AreEqual(2, piece.MaxHP);
        piece.Lower(StatKind.HP);
        Assert.AreEqual(1, piece.currentHP);
        Assert.IsFalse(piece.CanLower(StatKind.HP));
        piece.Lower(StatKind.HP);
        Assert.AreEqual(1, piece.currentHP);
        Assert.AreEqual(1, piece.MaxHP);
    }
}
