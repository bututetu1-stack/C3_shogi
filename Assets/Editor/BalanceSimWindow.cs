using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// バランステスト用の自動プレイを回すウィンドウ（メニュー「C3将棋 > バランステスト」）。
/// プレイ中に実行し、結果を BalanceReports/ に Markdown で書き出す。
/// </summary>
public class BalanceSimWindow : EditorWindow
{
    private int runs = 500;
    private int firstSeed = 1;
    private float thinkMs = BalanceSimulator.DefaultThinkMs;
    private string lastReportPath;
    private Vector2 scroll;
    private string lastReport;

    private static string ReportDir
    {
        get { return Path.Combine(Path.GetDirectoryName(Application.dataPath), "BalanceReports"); }
    }

    [MenuItem("C3将棋/バランステスト（自動プレイ）")]
    private static void Open()
    {
        GetWindow<BalanceSimWindow>("バランステスト");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("自動プレイで何周も回して、局ごとの勝率や駒の強さを集計します。", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("自軍もAIが指し、仲間はランダムに選びます。演出は出しません。", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space();

        runs = Mathf.Max(1, EditorGUILayout.IntField("周回数", runs));
        firstSeed = EditorGUILayout.IntField("乱数の種（最初の周）", firstSeed);
        thinkMs = Mathf.Max(1f, EditorGUILayout.FloatField("1手の思考時間（ms）", thinkMs));
        EditorGUILayout.Space();

        bool canRun = BalanceSimulator.CanRun;
        if (!canRun)
            EditorGUILayout.HelpBox("再生（プレイモード）を始めてから押してください。終わると盤はタイトル画面に戻ります。", MessageType.Info);

        using (new EditorGUI.DisabledScope(!canRun))
        {
            if (GUILayout.Button("自動プレイを開始", GUILayout.Height(32)))
                Run();
        }

        if (!string.IsNullOrEmpty(lastReportPath))
        {
            EditorGUILayout.Space();
            if (GUILayout.Button("報告を開く")) EditorUtility.OpenWithDefaultApp(lastReportPath);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(lastReport, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }
    }

    private void Run()
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string jsonl = Path.Combine(ReportDir, "sim_" + stamp + ".jsonl");
        try
        {
            BalanceSimulator.RunBatch(runs, firstSeed, thinkMs, jsonl, (done, total) =>
                !EditorUtility.DisplayCancelableProgressBar("バランステスト", done + " / " + total + " 周", (float)done / total));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        var records = BalanceSimulator.Load(jsonl);
        lastReport = BalanceSimulator.BuildReport(records, thinkMs);
        lastReportPath = Path.ChangeExtension(jsonl, ".md");
        File.WriteAllText(lastReportPath, lastReport, new System.Text.UTF8Encoding(false));
        Debug.Log("バランステストの報告: " + lastReportPath);

        // 盤を描き直すためにシーンを読み込み直す
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
