using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

/// <summary>防御の決まり（2026-10-12）: 能力のダメージにも防御が効くこと、RN のブロックの数え方</summary>
public class DefenseRulesTests
{
    private readonly List<Object> made = new List<Object>();

    private PieceInstance Piece(PieceType type, int atk, int def, int hp, Team team)
    {
        var d = ScriptableObject.CreateInstance<PieceData>();
        d.pieceType = type;
        d.displayName = type.ToString();
        d.baseATK = atk;
        d.baseDEF = def;
        d.baseHP = hp;
        d.moveDirections = new[] { new MoveDirection(0, 1, 1) };
        made.Add(d);
        return new PieceInstance(d, team, Vector2Int.zero);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in made) Object.DestroyImmediate(o);
        made.Clear();
    }

    [Test]
    public void AbilityDamage_IsReducedByDefense_WithCompensation()
    {
        bool legacy = BalanceTuning.LegacyPierce;
        int comp = BalanceTuning.PierceCompensation;
        BalanceTuning.LegacyPierce = false;
        BalanceTuning.PierceCompensation = 1;
        try
        {
            var bare = Piece(PieceType.Pawn, 1, 0, 3, Team.Enemy);
            var shield = Piece(PieceType.Gold, 1, 2, 3, Team.Enemy);
            Assert.AreEqual(2, CombatResolver.AbilityHitPreview(bare, 1), "防御0の相手には1+埋め合わせ1");
            Assert.AreEqual(0, CombatResolver.AbilityHitPreview(shield, 1), "防御2の相手には (1+1)−2 = 0");
            Assert.AreEqual(1, CombatResolver.AbilityHitPreview(shield, 2), "防御2の相手には (2+1)−2 = 1");
        }
        finally { BalanceTuning.LegacyPierce = legacy; BalanceTuning.PierceCompensation = comp; }
    }

    [Test]
    public void LegacyPierce_IgnoresDefense()
    {
        bool legacy = BalanceTuning.LegacyPierce;
        BalanceTuning.LegacyPierce = true;
        try
        {
            var shield = Piece(PieceType.Gold, 1, 2, 3, Team.Enemy);
            Assert.AreEqual(1, CombatResolver.AbilityHitPreview(shield, 1), "昔どおりなら防御を無視してそのまま");
        }
        finally { BalanceTuning.LegacyPierce = legacy; }
    }

    [Test]
    public void RnBlock_IsSubtractedAfterDefense()
    {
        var rn = Piece(PieceType.RN, 1, 1, 5, Team.Player);
        rn.block = 2;
        Assert.AreEqual(1, CombatResolver.AfterGuard(rn, 3), "防御を引いたあとの3から、ブロック2を引いて1");
        Assert.AreEqual(0, CombatResolver.AfterGuard(rn, 1), "ブロックより小さいダメージは0");
        rn.block = 0;
        Assert.AreEqual(3, CombatResolver.AfterGuard(rn, 3), "ブロックがなければそのまま");
    }
}
