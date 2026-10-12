using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

/// <summary>艦隊まわりの決まり（夜の改善ループで足したもの）: 対潜・潜航・大盾の正面</summary>
public class FleetRulesTests
{
    private readonly List<Object> made = new List<Object>();

    private PieceData Data(PieceType type, int atk, int def, int hp)
    {
        var d = ScriptableObject.CreateInstance<PieceData>();
        d.pieceType = type;
        d.displayName = type.ToString();
        d.baseATK = atk;
        d.baseDEF = def;
        d.baseHP = hp;
        d.moveDirections = new[] { new MoveDirection(0, 1, 1) };
        made.Add(d);
        return d;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in made) Object.DestroyImmediate(o);
        made.Clear();
    }

    [Test]
    public void EnemyPawnAndLance_HitTheSubmarine_WithAtLeastOne()
    {
        bool saved = BalanceTuning.EnemyAntiSub;
        BalanceTuning.EnemyAntiSub = true;
        try
        {
            var sub = new PieceInstance(Data(PieceType.KanmusuSS, 1, 3, 1), Team.Player, Vector2Int.zero);
            var pawn = new PieceInstance(Data(PieceType.Pawn, 1, 0, 2), Team.Enemy, new Vector2Int(0, 1));
            var lance = new PieceInstance(Data(PieceType.Lance, 2, 0, 2), Team.Enemy, new Vector2Int(0, 2));
            Assert.AreEqual(1, CombatResolver.CalcDamage(pawn, sub), "歩兵の爆雷は防御3でも最低1");
            Assert.AreEqual(1, CombatResolver.CalcDamage(lance, sub), "香車も対潜を持つ");
        }
        finally { BalanceTuning.EnemyAntiSub = saved; }
    }

    [Test]
    public void OtherEnemies_StillCannotHitTheSubmarine()
    {
        var sub = new PieceInstance(Data(PieceType.KanmusuSS, 1, 3, 1), Team.Player, Vector2Int.zero);
        var gold = new PieceInstance(Data(PieceType.Gold, 9, 0, 3), Team.Enemy, new Vector2Int(0, 1));
        Assert.AreEqual(0, CombatResolver.CalcDamage(gold, sub), "対潜を持たない駒の通常の攻撃は当たらない");
    }

    [Test]
    public void AntiSub_CanBeTurnedOff()
    {
        bool saved = BalanceTuning.EnemyAntiSub;
        BalanceTuning.EnemyAntiSub = false;
        try
        {
            var sub = new PieceInstance(Data(PieceType.KanmusuSS, 1, 3, 1), Team.Player, Vector2Int.zero);
            var pawn = new PieceInstance(Data(PieceType.Pawn, 5, 0, 2), Team.Enemy, new Vector2Int(0, 1));
            Assert.AreEqual(0, CombatResolver.CalcDamage(pawn, sub));
        }
        finally { BalanceTuning.EnemyAntiSub = saved; }
    }

    [Test]
    public void OnlyALivingSubmarine_IsSubmerged()
    {
        var sub = new PieceInstance(Data(PieceType.KanmusuSS, 1, 3, 1), Team.Player, Vector2Int.zero);
        var cv = new PieceInstance(Data(PieceType.KanmusuCV, 2, 2, 4), Team.Player, Vector2Int.zero);
        Assert.IsTrue(AbilitySystem.IsSubmerged(sub));
        Assert.IsFalse(AbilitySystem.IsSubmerged(cv));
        Assert.IsFalse(AbilitySystem.IsSubmerged(null));
        sub.isAlive = false;
        Assert.IsFalse(AbilitySystem.IsSubmerged(sub), "沈んだ潜水艦はもう通り抜けられない");
    }

    [Test]
    public void Shield_GuardsOnlyAgainstAttacksFromTheFront()
    {
        // 敵の大盾は下（自軍の側）を向いている
        var shield = new PieceInstance(Data(PieceType.Ootate, 1, 1, 4), Team.Enemy, new Vector2Int(2, 4));
        var front = new PieceInstance(Data(PieceType.Pawn, 5, 0, 2), Team.Player, new Vector2Int(2, 3));
        var side = new PieceInstance(Data(PieceType.Pawn, 5, 0, 2), Team.Player, new Vector2Int(3, 4));
        Assert.AreEqual(BalanceTuning.OotateGuard, CombatResolver.ShieldGuard(front, shield));
        Assert.AreEqual(0, CombatResolver.ShieldGuard(side, shield));
    }
}
