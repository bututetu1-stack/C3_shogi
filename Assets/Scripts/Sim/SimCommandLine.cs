using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 自動プレイ専用ビルドの入口。コマンドラインに -c3sim があれば、シーンの準備ができたところで
/// BalanceSimulator.RunBatch を回して終了する。画面なし（-batchmode -nographics）で何本も同時に動かせる。
/// 起動は docs/balance/run_parallel.js から。
///
///   -c3sim -runs 10 -seed 1 -out 出力.jsonl [-think 150] [-force Monin] [-teitoku]
///          [-set 名前=値]...        BalanceTuning の値を変える（int / bool / int[] はカンマ区切り、空なら空配列）
///          [-piece 駒=JSON]...      PieceData の一部を JsonUtility の形で上書き（例: C3={"baseHP":10}）
/// 終了コード: 0 = 回し終えた、1 = 失敗（理由は標準エラーとログ）
/// </summary>
public class SimCommandLine : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), "-c3sim") < 0) return;
        new GameObject("SimCommandLine").AddComponent<SimCommandLine>();
    }

    private IEnumerator Start()
    {
        // 各マネージャの Start（タイトル表示など）が済むのを待つ
        yield return null;
        yield return null;

        int exitCode = 0;
        try
        {
            string[] args = Environment.GetCommandLineArgs();
            int runs = int.Parse(Arg(args, "-runs", "1"));
            int seed = int.Parse(Arg(args, "-seed", "1"));
            float think = float.Parse(Arg(args, "-think", BalanceSimulator.DefaultThinkMs.ToString()));
            string output = Arg(args, "-out", null);
            if (string.IsNullOrEmpty(output)) throw new ArgumentException("-out がありません");

            SimOptions options = null;
            string force = Arg(args, "-force", null);
            if (!string.IsNullOrEmpty(force))
                options = new SimOptions { forcePick = (PieceType)Enum.Parse(typeof(PieceType), force), promoteAtStart = Array.IndexOf(args, "-teitoku") >= 0 };

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-set") SetTuning(args[i + 1]);
                else if (args[i] == "-piece") SetPiece(args[i + 1]);
            }

            int done = BalanceSimulator.RunBatch(runs, seed, think, output, null, options);
            Console.Out.WriteLine("c3sim done=" + done);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Console.Error.WriteLine("c3sim failed: " + e);
            exitCode = 1;
        }
        Application.Quit(exitCode);
    }

    private static string Arg(string[] args, string name, string fallback)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    /// <summary>「名前=値」で BalanceTuning の静的フィールドを書き換える</summary>
    private static void SetTuning(string assignment)
    {
        int eq = assignment.IndexOf('=');
        string name = assignment.Substring(0, eq), value = assignment.Substring(eq + 1);
        FieldInfo field = typeof(BalanceTuning).GetField(name, BindingFlags.Public | BindingFlags.Static);
        if (field == null) throw new ArgumentException("BalanceTuning に " + name + " がありません");
        if (field.FieldType == typeof(int)) field.SetValue(null, int.Parse(value));
        else if (field.FieldType == typeof(bool)) field.SetValue(null, bool.Parse(value));
        else if (field.FieldType == typeof(int[]))
            field.SetValue(null, value.Length == 0 ? new int[0] : Array.ConvertAll(value.Split(','), int.Parse));
        else throw new ArgumentException(name + " の型には対応していません");
    }

    /// <summary>「駒=JSON」で PieceData の一部を上書きする（ビルド中のアセットは保存されないので元に戻す必要はない）</summary>
    private static void SetPiece(string assignment)
    {
        int eq = assignment.IndexOf('=');
        PieceType type = (PieceType)Enum.Parse(typeof(PieceType), assignment.Substring(0, eq));
        PieceData data = BoardManager.Instance.GetPieceDataByType(type);
        if (data == null) throw new ArgumentException(type + " の駒のデータがありません");
        JsonUtility.FromJsonOverwrite(assignment.Substring(eq + 1), data);
    }
}
