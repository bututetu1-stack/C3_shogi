using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RunRosterTests
{
    private readonly List<Object> created = new List<Object>();

    private PieceData Make(PieceType type, Rarity rarity)
    {
        var d = ScriptableObject.CreateInstance<PieceData>();
        d.pieceType = type;
        d.rarity = rarity;
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
    public void Stars_FollowRarityThresholds()
    {
        // ブロンズは早く、激レアは遅く★が上がる
        Assert.AreEqual(0, RunRoster.StarsFor(Rarity.Bronze, BalanceTuning.StarXpBronze[0] - 1));
        Assert.AreEqual(1, RunRoster.StarsFor(Rarity.Bronze, BalanceTuning.StarXpBronze[0]));
        Assert.AreEqual(3, RunRoster.StarsFor(Rarity.Bronze, BalanceTuning.StarXpBronze[2]));
        Assert.AreEqual(2, RunRoster.StarsFor(Rarity.SuperRare, BalanceTuning.StarXpSuperRare[1]));
        Assert.AreEqual(3, RunRoster.StarsFor(Rarity.Rare, 999), "★は3で止まる");
        Assert.Less(BalanceTuning.StarXpBronze[2], BalanceTuning.StarXpSuperRare[2]);
    }

    [Test]
    public void AddXp_RaisesStarsAndNextThreshold()
    {
        var roster = new RunRoster();
        RunMember m = roster.Add(Make(PieceType.Boku, Rarity.Rare));
        Assert.AreEqual(BalanceTuning.StarXpRare[0], RunRoster.NextThreshold(m));
        roster.AddXp(m, BalanceTuning.StarXpRare[1]);
        Assert.AreEqual(2, m.stars);
        Assert.AreEqual(BalanceTuning.StarXpRare[2], RunRoster.NextThreshold(m));
        roster.AddXp(m, 100);
        Assert.AreEqual(3, m.stars);
        Assert.AreEqual(-1, RunRoster.NextThreshold(m));
    }

    [Test]
    public void Feats_AreCappedPerStage()
    {
        var roster = new RunRoster();
        PieceData data = Make(PieceType.Kei, Rarity.Bronze);
        RunMember m = roster.Add(data);
        var piece = new PieceInstance(data, Team.Player, Vector2Int.zero);
        for (int i = 0; i < 10; i++) roster.OnFeat(piece);
        Assert.AreEqual(BalanceTuning.FeatXpMaxPerStage, m.xp, "1局の活躍の練度には上限がある");
        roster.BeginStage();
        roster.OnFeat(piece);
        Assert.AreEqual(BalanceTuning.FeatXpMaxPerStage + BalanceTuning.XpPerFeat, m.xp, "次の局ではまたたまる");
    }

    [Test]
    public void Kills_CountOnlyEnemiesByMembers()
    {
        var roster = new RunRoster();
        PieceData nako = Make(PieceType.Nako, Rarity.Rare);
        PieceData pawn = Make(PieceType.Pawn, Rarity.Normal);
        PieceData chuka = Make(PieceType.Chuka, Rarity.Rare);
        RunMember m = roster.Add(nako);
        var killer = new PieceInstance(nako, Team.Player, Vector2Int.zero);
        roster.OnKill(killer, new PieceInstance(pawn, Team.Enemy, Vector2Int.one));
        roster.OnKill(killer, new PieceInstance(chuka, Team.Enemy, Vector2Int.one));
        roster.OnKill(killer, new PieceInstance(pawn, Team.Player, Vector2Int.one));
        Assert.AreEqual(1, m.kills);
        Assert.AreEqual(BalanceTuning.XpPerKill, m.xp);
        // 部員でない駒（歩）が倒しても名簿は変わらない
        roster.OnKill(new PieceInstance(pawn, Team.Player, Vector2Int.zero), new PieceInstance(pawn, Team.Enemy, Vector2Int.one));
        Assert.AreEqual(1, roster.Members.Count);
    }

    [Test]
    public void ApplyStars_AddsEachStarOnce()
    {
        PieceData data = Make(PieceType.Yuu, Rarity.SuperRare);
        data.baseATK = 2; data.baseDEF = 0; data.baseHP = 3;
        var p = new PieceInstance(data, Team.Player, Vector2Int.zero);
        RunRoster.ApplyStars(p, 0, 3);
        Assert.AreEqual(2 + BalanceTuning.StarATK, p.ATK);
        Assert.AreEqual(BalanceTuning.StarDEF, p.DEF);
        Assert.AreEqual(3 + BalanceTuning.StarHP, p.MaxHP);
    }

    [Test]
    public void TrainCards_OnlyForOwnedMembersBelowThreeStars()
    {
        PieceData boku = Make(PieceType.Boku, Rarity.Rare);
        PieceData sn = Make(PieceType.SN, Rarity.SuperRare);
        var roster = new RunRoster();
        roster.Add(boku);
        RunMember snMember = roster.Add(sn);
        roster.AddXp(snMember, 100);   // ★3 は鍛えられない
        var owned = new List<PieceData> { boku, sn };
        var all = new[] { Make(PieceType.C3, Rarity.Normal), boku, sn };

        bool sawTrain = false;
        for (int i = 0; i < 80; i++)
        {
            // 新しい部員がいない局では、空いた枠が鍛える札で埋まる
            foreach (var option in PiecePool.DrawOptions(all, 3, owned, 5, true, roster))
            {
                if (!option.IsTrain) continue;
                sawTrain = true;
                Assert.AreEqual(boku, option.train, "★3の部員や仲間でない部員は鍛える札に出ない");
            }
        }
        Assert.IsTrue(sawTrain);
    }
}
