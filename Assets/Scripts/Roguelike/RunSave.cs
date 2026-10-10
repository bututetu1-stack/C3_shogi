using System.IO;
using UnityEngine;

/// <summary>中断した周の保存内容（JsonUtility で書き出す）</summary>
[System.Serializable]
public class RunSaveData
{
    public int version = 1;
    /// <summary>"battle"（仲間選びを終えて対局を始める前）または "next"（局に勝って次の局の前）</summary>
    public string phase;
    public int stage;
    public string[] owned;          // 仲間にした駒（PieceType の名前）
    public RunMember[] members;     // 部員の練度
    public int runATK, runDEF, runHP, runC3HP;
    public int damageControl;
    public int bonusUpgradeStage;
    public int totalKills, totalMoves, stagesCleared;
}

/// <summary>中断と続きから（Application.persistentDataPath/run.json）</summary>
public static class RunSave
{
    private static string FilePath { get { return Path.Combine(Application.persistentDataPath, "run.json"); } }

    public static bool Exists { get { return File.Exists(FilePath); } }

    public static void Save(RunSaveData data)
    {
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(data)); }
        catch (System.Exception e) { Debug.LogWarning("中断データを保存できませんでした: " + e.Message); }
    }

    public static RunSaveData Load()
    {
        try { return Exists ? JsonUtility.FromJson<RunSaveData>(File.ReadAllText(FilePath)) : null; }
        catch (System.Exception e) { Debug.LogWarning("中断データを読めませんでした: " + e.Message); return null; }
    }

    public static void Delete()
    {
        try { if (Exists) File.Delete(FilePath); }
        catch (System.Exception e) { Debug.LogWarning("中断データを消せませんでした: " + e.Message); }
    }
}
