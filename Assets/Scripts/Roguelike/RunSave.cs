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
    public bool noPawns;            // 強化「歩兵を消す」
    public int[] variants;          // 局ごとの布陣の型（周の初めに決めたもの）
    public int secretFund;          // 光春の裏金
    public int fleetSWins;          // この周で艦隊がS勝利した回数
    public string[] ringedClasses;  // ケッコンカッコカリした艦種（PieceType の名前）
    public int bonusUpgradeStage;
    public int enemyHp, enemyAtk;   // 強敵に挑む: この局の敵の体力・攻撃の上乗せ（"battle" のときだけ）
    public int totalKills, totalMoves, stagesCleared;
}

/// <summary>中断と続きから（Application.persistentDataPath/run.json。ブラウザ版は PlayerPrefs）</summary>
public static class RunSave
{
#if UNITY_WEBGL && !UNITY_EDITOR
    // ブラウザ版: ファイルは書き戻しを頼まないと残らないので、PlayerPrefs（Save でブラウザの IndexedDB に入る）に置く
    private const string Key = "c3_run_save";

    public static bool Exists { get { return PlayerPrefs.HasKey(Key); } }
    private static string ReadText() { return PlayerPrefs.GetString(Key); }
    private static void WriteText(string text) { PlayerPrefs.SetString(Key, text); PlayerPrefs.Save(); }
    private static void DeleteText() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
#else
    private static string FilePath { get { return Path.Combine(Application.persistentDataPath, "run.json"); } }

    public static bool Exists { get { return File.Exists(FilePath); } }
    private static string ReadText() { return File.ReadAllText(FilePath); }
    private static void WriteText(string text) { File.WriteAllText(FilePath, text); }
    private static void DeleteText() { File.Delete(FilePath); }
#endif

    public static void Save(RunSaveData data)
    {
        try { WriteText(JsonUtility.ToJson(data)); }
        catch (System.Exception e) { Debug.LogWarning("中断データを保存できませんでした: " + e.Message); }
    }

    public static RunSaveData Load()
    {
        try { return Exists ? JsonUtility.FromJson<RunSaveData>(ReadText()) : null; }
        catch (System.Exception e) { Debug.LogWarning("中断データを読めませんでした: " + e.Message); return null; }
    }

    public static void Delete()
    {
        try { if (Exists) DeleteText(); }
        catch (System.Exception e) { Debug.LogWarning("中断データを消せませんでした: " + e.Message); }
    }
}
