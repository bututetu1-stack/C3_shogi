using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RunFlowTests
{
    [Test]
    public void StageEvents_AreDistinct_AndSkipGrowthWithoutMembers()
    {
        for (int i = 0; i < 50; i++)
        {
            List<StageEventKind> withMembers = StageEvents.Draw(2, true);
            Assert.AreEqual(2, withMembers.Count);
            Assert.AreNotEqual(withMembers[0], withMembers[1], "同じできごとは並ばない");

            foreach (var kind in StageEvents.Draw(2, false))
            {
                Assert.AreNotEqual(StageEventKind.Camp, kind, "部員がいないと練度のできごとは出ない");
                Assert.AreNotEqual(StageEventKind.Sparring, kind);
            }
        }
        foreach (StageEventKind kind in System.Enum.GetValues(typeof(StageEventKind)))
        {
            Assert.IsNotEmpty(StageEvents.Title(kind));
            Assert.IsNotEmpty(StageEvents.Description(kind));
        }
    }

    [Test]
    public void SaveData_RoundTripsThroughJson()
    {
        var data = new RunSaveData
        {
            phase = "battle",
            stage = 7,
            owned = new[] { "Konishiki", "Boku" },
            members = new[]
            {
                new RunMember { type = PieceType.Konishiki, rarity = Rarity.Rare, xp = 9, stars = 2, kills = 3 },
                new RunMember { type = PieceType.Boku, rarity = Rarity.Rare, xp = 15, stars = 3, kills = 0 }
            },
            runATK = 1, runC3HP = 4, damageControl = 1, bonusUpgradeStage = 8, enemyHp = 1, totalKills = 20, stagesCleared = 6
        };
        RunSaveData back = JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(data));
        Assert.AreEqual("battle", back.phase);
        Assert.AreEqual(7, back.stage);
        CollectionAssert.AreEqual(data.owned, back.owned);
        Assert.AreEqual(2, back.members.Length);
        Assert.AreEqual(PieceType.Boku, back.members[1].type);
        Assert.AreEqual(3, back.members[1].stars);
        Assert.AreEqual(4, back.runC3HP);
        Assert.AreEqual(8, back.bonusUpgradeStage);
        Assert.AreEqual(1, back.enemyHp, "強敵に挑んだ分も残る");

        var roster = new RunRoster();
        roster.Restore(back.members);
        Assert.AreEqual(2, roster.StarsOf(PieceType.Konishiki));
        Assert.AreEqual(3, roster.Get(PieceType.Konishiki).kills);
    }
}
