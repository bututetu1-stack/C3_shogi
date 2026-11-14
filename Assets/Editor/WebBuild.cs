using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// ブラウザ版（unityroom に置く）を Builds/WebGL に作る。
/// unityroom には Builds/WebGL/Build の4つ（.loader.js・.data.gz・.framework.js.gz・.wasm.gz）を上げる。
/// 圧縮は Gzip（unityroom は Brotli の .br を読めないことがある）、解凍のフォールバックはなし。
/// 作ると、エディタの向き先は Web のままになる（自動プレイ用のビルドを作れば Windows に戻る）
/// </summary>
public static class WebBuild
{
    public const string OutputPath = "Builds/WebGL";

    [MenuItem("C3将棋/ブラウザ版（unityroom）を作る")]
    public static void BuildFromMenu()
    {
        string result = Build();
        EditorUtility.DisplayDialog("ブラウザ版", result, "OK");
    }

    /// <summary>ビルドして結果を返す（isuzu-unity-cli の execute_code からも呼ぶ）</summary>
    public static string Build()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = false;
        // 対人戦で相手の手を待つあいだ、ページの別の所を触っても止まらないように
        PlayerSettings.runInBackground = true;
        ShrinkTexturesForWeb();

        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = OutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        string text = summary.result + " " + OutputPath + "（" + summary.totalTime.TotalSeconds.ToString("0") + "秒、エラー " + summary.totalErrors + "）";
        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("ブラウザ版のビルドに失敗: " + text);
            return text;
        }
        // unityroom に上げるファイルと大きさ
        string dir = Path.Combine(OutputPath, "Build");
        if (Directory.Exists(dir))
            foreach (string f in Directory.GetFiles(dir))
                text += "\n" + Path.GetFileName(f) + " " + (new FileInfo(f).Length / 1048576f).ToString("0.0") + "MB";
        return text;
    }

    /// <summary>
    /// ブラウザ版だけ、Resources の画像を縮めて圧縮する（Windows 版の設定は変えない）。
    /// 1254px の画像は辺が4の倍数でないので圧縮されず、データが 100MB を超えたため。
    /// エフェクトと駒の地は読み込むときに 512px に縮めている（ArtTexture.Load）ので、512 にしても見た目は変わらない。
    /// 新しく置いた画像も、ビルドのたびにここでそろえる
    /// </summary>
    private static void ShrinkTexturesForWeb()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Effects", "Assets/Resources/Art" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            int size = path.StartsWith("Assets/Resources/Effects/") || path.EndsWith("/PieceBody.png") ? 512 : 1024;
            TextureImporterPlatformSettings s = importer.GetPlatformTextureSettings("WebGL");
            if (s.overridden && s.maxTextureSize == size && s.textureCompression == TextureImporterCompression.Compressed) continue;
            s.overridden = true;
            s.maxTextureSize = size;
            s.format = TextureImporterFormat.Automatic;
            s.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(s);
            importer.SaveAndReimport();
        }
    }
}
