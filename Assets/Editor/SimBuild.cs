using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 自動プレイ専用の Windows ビルド（Builds/Sim/C3Sim.exe）を作る。
/// コードや駒のデータを変えたら作り直してから docs/balance/run_parallel.js を回す
/// </summary>
public static class SimBuild
{
    public const string OutputPath = "Builds/Sim/C3Sim.exe";

    [MenuItem("C3将棋/自動プレイ用のビルドを作る")]
    public static void BuildFromMenu()
    {
        string result = Build();
        EditorUtility.DisplayDialog("自動プレイ用のビルド", result, "OK");
    }

    /// <summary>ビルドして結果を1行で返す（isuzu-unity-cli の execute_code からも呼ぶ）</summary>
    public static string Build()
    {
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        string text = summary.result + " " + OutputPath + "（" + summary.totalTime.TotalSeconds.ToString("0") + "秒、エラー " + summary.totalErrors + "）";
        if (summary.result != BuildResult.Succeeded) Debug.LogError("自動プレイ用のビルドに失敗: " + text);
        return text;
    }
}
