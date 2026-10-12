using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class StageLayoutTests
{
    private static IEnumerable<int> AllStages()
    {
        for (int stage = 1; stage <= StageManager.MaxStages; stage++)
            yield return stage;
    }

    [TestCaseSource(nameof(AllStages))]
    public void EnemyLayout_HasNoOverlapsAndStaysInEnemyCamp(int stage)
    {
        int size = StageManager.GetBoardSizeForStage(stage);
        // 布陣の型（第9版）もすべて同じ決まりで確かめる
        for (int v = 0; v < StageManager.VariantCount(stage); v++)
        {
            var layout = StageManager.GetEnemyLayout(stage, size, v);
            var used = new HashSet<Vector2Int>();
            int c3Count = 0;
            string where = "型" + v + " ";

            foreach (var p in layout)
            {
                Assert.That(p.position.x, Is.InRange(0, size - 1), where + p.type + " が盤外 " + p.position);
                Assert.That(p.position.y, Is.InRange(0, size - 1), where + p.type + " が盤外 " + p.position);
                // プレイヤーの配置エリア（下3段）とは重ならない
                Assert.That(p.position.y, Is.GreaterThanOrEqualTo(3), where + p.type + " が自陣に食い込んでいる " + p.position);
                Assert.IsTrue(used.Add(p.position), where + p.type + " の配置マスが重複 " + p.position);
                if (p.type == PieceType.C3) c3Count++;
            }

            Assert.AreEqual(1, c3Count, where + "敵C3はちょうど1体");
            if (v > 0) Assert.IsNotNull(StageManager.VariantName(stage, v), where + "型の名前がない");
        }
    }

    [TestCaseSource(nameof(AllStages))]
    public void VariantZero_IsTheOriginalLayout(int stage)
    {
        int size = StageManager.GetBoardSizeForStage(stage);
        var a = StageManager.GetEnemyLayout(stage, size);
        var b = StageManager.GetEnemyLayout(stage, size, 0);
        Assert.AreEqual(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.AreEqual(a[i].type, b[i].type);
            Assert.AreEqual(a[i].position, b[i].position);
        }
    }

    [Test]
    public void ShogiStageAndFinalBoss_KeepOneLayout()
    {
        Assert.AreEqual(1, StageManager.VariantCount(5), "将棋の陣は本将棋のまま");
        Assert.AreEqual(1, StageManager.VariantCount(15), "魔王降臨はそのまま");
    }

    [Test]
    public void ObstaclesAreNotCountedAsFighters()
    {
        Assert.IsTrue(AbilitySystem.IsObstacle(PieceType.Kyogan));
        Assert.IsTrue(AbilitySystem.IsObstacle(PieceType.Kugutsu));
        Assert.IsFalse(AbilitySystem.IsObstacle(PieceType.Kekkai));
    }

    [Test]
    public void EveryPieceTypeUsedInStages_HasPieceData()
    {
        var available = new HashSet<PieceType>();
        foreach (string guid in AssetDatabase.FindAssets("t:PieceData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<PieceData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null) available.Add(data.pieceType);
        }

        foreach (int stage in AllStages())
        {
            int size = StageManager.GetBoardSizeForStage(stage);
            for (int v = 0; v < StageManager.VariantCount(stage); v++)
                foreach (var p in StageManager.GetEnemyLayout(stage, size, v))
                    Assert.IsTrue(available.Contains(p.type), "ステージ" + stage + " 型" + v + " の " + p.type + " に PieceData がない");
        }
        Assert.IsTrue(available.Contains(PieceType.Kitsunebi), "妖狐が残す狐火に PieceData がない");
    }

    [Test]
    public void EveryPieceData_HasNamesAndMoves()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:PieceData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<PieceData>(path);
            Assert.IsFalse(string.IsNullOrEmpty(data.displayName), path + " の displayName が空");
            Assert.IsFalse(string.IsNullOrEmpty(data.pieceName), path + " の pieceName が空");
            if (!data.isImmovable && data.pieceType != PieceType.C3)
                Assert.IsTrue(data.moveDirections != null && data.moveDirections.Length > 0, path + " に移動方向がない");
            if (data.canPromote)
                Assert.IsFalse(string.IsNullOrEmpty(data.promotedDisplayName), path + " の成り後の表示名が空");
        }
    }
}
