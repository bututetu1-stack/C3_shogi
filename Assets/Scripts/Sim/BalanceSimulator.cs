using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>1局ぶんの自動プレイの記録</summary>
[Serializable]
public class SimStageRecord
{
    public int stage;
    public string outcome;          // Won / JudgedWin / Lost / JudgedLoss / Timeout
    public string lossCause;        // 負けたとき: C3撃破 / 全滅 / 判定
    public int moves;               // 決着までの手数
    public string pick;             // 選んだ仲間・強化（候補がなければ空）
    public string pick2;            // 2枚選べる局の2枚目
    public string[] offered;        // 候補
    public string[] roster;         // 対局開始時の自軍（C3・歩・召喚物を除く）
    public string[] survivors;      // 決着時に残っていた自軍（同上）
    public string[] kills;          // 自軍が倒した敵ごとの、倒した駒
    public string[] victims;        // kills と同じ順の、倒された敵
    public string[] losses;         // 倒された自軍の駒
    public string[] lossBy;         // losses と同じ順の、倒した敵
    public string[] promotions;     // 成った自軍の駒
    public int playerC3HP;
    public int enemyLeft;           // 決着時に残っていた敵（C3を除く）
}

/// <summary>1周ぶんの自動プレイの記録</summary>
[Serializable]
public class SimRunRecord
{
    public int seed;
    public int reached;             // 最後に戦った局
    public bool cleared;            // 第十五局まで勝ち抜いた
    public float seconds;
    // AIが読み切れた深さの内訳（敵が4手先まで読めた手数／敵の全手数、自軍も同様）
    public int enemyDeepMoves, enemyMoves, playerDeepMoves, playerMoves;
    public List<SimStageRecord> stages = new List<SimStageRecord>();
}

/// <summary>自動プレイの条件（駒の強さを同じ条件で比べるため）</summary>
public class SimOptions
{
    /// <summary>第一局で必ずこの駒を仲間にする（候補の抽選は通常どおり行い、選ぶ札だけ差し替える）</summary>
    public PieceType? forcePick;
    /// <summary>forcePick の駒を各局の開始時に成らせる（成った姿の強さを測る）</summary>
    public bool promoteAtStart;
}

/// <summary>
/// バランステスト用の自動プレイ。プレイ中のシーンの GameManager・AbilitySystem・AI をそのまま使い、
/// 演出を出さずに1周（仲間選択 → 対局 × 最大15局）を最後まで回す。
/// 自軍も AI（敵より強い設定）が指し、仲間選択は候補からランダムに選ぶ。
/// </summary>
public static class BalanceSimulator
{
    /// <summary>1手の思考時間（ミリ秒）。本番の敵と同じ150ms（読みはたいていその前に終わるので速さはほぼ変わらない）</summary>
    public const float DefaultThinkMs = 150f;

    public static bool CanRun
    {
        get
        {
            return Application.isPlaying && GameManager.Instance != null && BoardManager.Instance != null
                && StageManager.Instance != null && SimpleAI.Instance != null;
        }
    }

    /// <summary>
    /// runs 周を回し、1周ごとに JSON を1行ずつ jsonlPath に追記する（途中で止めてもそこまでの記録は残る）。
    /// onProgress(終わった周, 全体) が false を返したら中断する
    /// </summary>
    public static int RunBatch(int runs, int firstSeed, float thinkMs, string jsonlPath, Func<int, int, bool> onProgress = null, SimOptions options = null)
    {
        if (!CanRun) throw new InvalidOperationException("プレイ中のシーンで実行してください");
        string dir = Path.GetDirectoryName(jsonlPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        int done = 0;
        GameSim.Headless = true;
        try
        {
            for (int i = 0; i < runs; i++)
            {
                if (onProgress != null && !onProgress(i, runs)) break;
                SimRunRecord record = RunOne(firstSeed + i, thinkMs, options);
                File.AppendAllText(jsonlPath, JsonUtility.ToJson(record) + "\n", new UTF8Encoding(false));
                done++;
            }
        }
        finally
        {
            GameSim.Headless = false;
            GameSim.BeginStageStats();
        }
        return done;
    }

    /// <summary>1周を最後まで回す</summary>
    public static SimRunRecord RunOne(int seed, float thinkMs, SimOptions simOptions = null)
    {
        GameManager gm = GameManager.Instance;
        StageManager sm = StageManager.Instance;
        BoardManager bm = BoardManager.Instance;
        SimpleAI ai = SimpleAI.Instance;

        var clock = System.Diagnostics.Stopwatch.StartNew();
        UnityEngine.Random.InitState(seed);
        var run = new SimRunRecord { seed = seed };
        gm.SimBeginRun();

        while (true)
        {
            var rec = new SimStageRecord { stage = sm.currentStage, pick = "" };

            // 仲間選択（候補からランダム。2枚選べる局は2回）
            int picks = GameManager.PicksForStage(sm.currentStage);
            for (int k = 0; k < picks; k++)
            {
                List<DraftOption> options = gm.SimDrawOptions();
                if (k == 0) rec.offered = options.Select(OptionName).ToArray();
                if (options.Count == 0) break;
                DraftOption pick = options[UnityEngine.Random.Range(0, options.Count)];
                if (k == 0 && simOptions != null && simOptions.forcePick.HasValue && sm.currentStage == 1)
                    pick = DraftOption.Piece(bm.GetPieceDataByType(simOptions.forcePick.Value));
                if (k == 0) rec.pick = OptionName(pick);
                else rec.pick2 = OptionName(pick);
                gm.SimApplyOption(pick);
            }
            if (rec.offered == null) rec.offered = new string[0];

            GameSim.BeginStageStats();
            GameSim.RunSync(gm.SimStartBattle());
            if (simOptions != null && simOptions.promoteAtStart && simOptions.forcePick.HasValue)
            {
                PieceInstance forced = bm.GetTeamPieces(Team.Player)
                    .FirstOrDefault(p => p.data.pieceType == simOptions.forcePick.Value && !p.isPromoted);
                if (forced != null) gm.PromotePiece(forced);
            }
            rec.roster = FighterNames(bm, Team.Player);

            int guard = 0;
            while (gm.SimOutcome == SimBattleOutcome.None && guard++ < GameManager.MoveLimit * 3)
            {
                PieceInstance piece;
                MoveValidator.MoveResult move;
                Team team = gm.currentTurn;
                if (ai.TryChooseMove(team, thinkMs, out piece, out move))
                {
                    bool deep = ai.LastCompletedDepth >= 4;
                    if (team == Team.Enemy) { run.enemyMoves++; if (deep) run.enemyDeepMoves++; }
                    else { run.playerMoves++; if (deep) run.playerDeepMoves++; }
                    GameSim.RunSync(CombatResolver.ExecuteMove(piece, move));
                }
                GameSim.RunSync(gm.SimEndTurn());
            }

            SimBattleOutcome outcome = gm.SimOutcome;
            rec.outcome = outcome == SimBattleOutcome.None ? "Timeout" : outcome.ToString();
            rec.moves = gm.MoveCount;
            rec.survivors = FighterNames(bm, Team.Player);
            rec.kills = GameSim.StageKills.ToArray();
            rec.victims = GameSim.StageKillVictims.ToArray();
            rec.losses = GameSim.StageLosses.ToArray();
            rec.lossBy = GameSim.StageLossKillers.ToArray();
            rec.promotions = GameSim.StagePromotions.ToArray();
            PieceInstance c3 = bm.FindC3(Team.Player);
            rec.playerC3HP = c3 != null ? c3.currentHP : 0;
            rec.enemyLeft = bm.GetTeamPieces(Team.Enemy).Count(p => p.data.pieceType != PieceType.C3);
            bool won = outcome == SimBattleOutcome.Won || outcome == SimBattleOutcome.JudgedWin;
            if (!won)
                rec.lossCause = outcome == SimBattleOutcome.JudgedLoss ? "判定" : (c3 == null ? "C3撃破" : (outcome == SimBattleOutcome.None ? "時間切れ" : "全滅"));
            run.stages.Add(rec);
            run.reached = sm.currentStage;

            if (!won) break;
            if (sm.IsLastStage()) { run.cleared = true; break; }
            gm.SimNextStage();
        }

        bm.ClearAll();
        run.seconds = (float)clock.Elapsed.TotalSeconds;
        return run;
    }

    private static string OptionName(DraftOption option)
    {
        if (option == null) return "";
        return option.piece != null ? option.piece.displayName : "強化:" + option.Title;
    }

    /// <summary>C3・歩・召喚物を除いた自軍の駒の名前</summary>
    private static string[] FighterNames(BoardManager bm, Team team)
    {
        return bm.GetTeamPieces(team)
            .Where(p => p.data.pieceType != PieceType.C3 && p.data.pieceType != PieceType.Pawn
                && p.data.pieceType != PieceType.Chuka && p.data.pieceType != PieceType.Dopa)
            .Select(p => p.data.displayName).ToArray();
    }

    // ================================================================
    // 集計
    // ================================================================

    public static List<SimRunRecord> Load(string jsonlPath)
    {
        var list = new List<SimRunRecord>();
        if (!File.Exists(jsonlPath)) return list;
        foreach (string line in File.ReadAllLines(jsonlPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            list.Add(JsonUtility.FromJson<SimRunRecord>(line));
        }
        return list;
    }

    /// <summary>記録を読みやすい Markdown の報告にまとめる</summary>
    public static string BuildReport(List<SimRunRecord> runs, float thinkMs)
    {
        var sb = new StringBuilder();
        int n = runs.Count;
        if (n == 0) return "記録がありません。";
        int maxStage = StageManager.MaxStages;

        int cleared = runs.Count(r => r.cleared);
        sb.AppendLine("# C3将棋 バランステスト");
        sb.AppendLine();
        sb.AppendLine("- 周回数: " + n + "（1手の思考 " + thinkMs + "ms、仲間選択はランダム）");
        sb.AppendLine("- 全15局クリア: " + cleared + "周（" + Pct(cleared, n) + "）");
        sb.AppendLine("- 平均到達局: " + runs.Average(r => r.reached).ToString("0.0"));
        sb.AppendLine("- 1周の平均時間: " + runs.Average(r => r.seconds).ToString("0.0") + "秒");
        int em = runs.Sum(r => r.enemyMoves), pm = runs.Sum(r => r.playerMoves);
        sb.AppendLine("- 4手先まで読み切れた手の割合: 敵 " + Pct(runs.Sum(r => r.enemyDeepMoves), em) + "、自軍 " + Pct(runs.Sum(r => r.playerDeepMoves), pm)
            + "（敵は第一局〜第四局はもともと2手先までしか読まない）");
        sb.AppendLine();

        // ---- 局ごと ----
        sb.AppendLine("## 局ごとの結果");
        sb.AppendLine();
        sb.AppendLine("| 局 | 挑戦 | 勝率 | うち判定勝ち | 敗因（C3撃破/全滅/判定） | 平均手数 | 勝ったときのC3残り体力 |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        for (int s = 1; s <= maxStage; s++)
        {
            var recs = runs.SelectMany(r => r.stages).Where(x => x.stage == s).ToList();
            if (recs.Count == 0) continue;
            var wins = recs.Where(IsWin).ToList();
            int judgedWins = recs.Count(x => x.outcome == "JudgedWin");
            int byC3 = recs.Count(x => x.lossCause == "C3撃破");
            int byWipe = recs.Count(x => x.lossCause == "全滅");
            int byJudge = recs.Count(x => x.lossCause == "判定" || x.lossCause == "時間切れ");
            sb.AppendLine("| " + s + " | " + recs.Count + " | " + Pct(wins.Count, recs.Count) + " | " + judgedWins
                + " | " + byC3 + " / " + byWipe + " / " + byJudge
                + " | " + recs.Average(x => x.moves).ToString("0")
                + " | " + (wins.Count > 0 ? wins.Average(x => x.playerC3HP).ToString("0.0") : "-") + " |");
        }
        sb.AppendLine();

        // ---- 仲間の駒・強化カード ----
        // 強化カードやレアな駒は後半ほど出るので、「選んだ周の到達局」で比べると後半に強く見えてしまう。
        // そこで局ごとに「その札を持っていた対局」と「持っていなかった対局」の勝率を比べ、その差を平均する
        var pickNames = runs.SelectMany(r => r.stages).Where(x => !string.IsNullOrEmpty(x.pick)).Select(x => x.pick).Distinct().ToList();
        var entries = runs.SelectMany(r => r.stages.Select(x => new { run = r, rec = x })).ToList();
        sb.AppendLine("## 仲間・強化カードの強さ");
        sb.AppendLine();
        sb.AppendLine("「勝率の差」は、同じ局で比べたときの「その札を持っていた対局の勝率 − 持っていなかった対局の勝率」の平均。正なら勝ちやすくなる札。");
        sb.AppendLine("撃破数・被撃破率は、その駒が盤上にいた対局1回あたりの値。");
        sb.AppendLine();
        sb.AppendLine("| 札 | 提示 | 選択 | 勝率の差 | 撃破数/局 | 被撃破率 |");
        sb.AppendLine("|---|---|---|---|---|---|");
        var rows = new List<KeyValuePair<double, string>>();
        foreach (string name in pickNames)
        {
            int offered = runs.SelectMany(r => r.stages).Count(x => x.offered != null && x.offered.Contains(name));
            int chosen = runs.SelectMany(r => r.stages).Count(x => x.pick == name);
            double sum = 0, weight = 0;
            for (int s = 1; s <= maxStage; s++)
            {
                var at = entries.Where(e => e.rec.stage == s).ToList();
                var with = at.Where(e => e.run.stages.Any(x => x.stage <= s && x.pick == name)).ToList();
                var without = at.Where(e => !e.run.stages.Any(x => x.stage <= s && x.pick == name)).ToList();
                int w = Math.Min(with.Count, without.Count);
                if (w == 0) continue;
                double rateWith = (double)with.Count(e => IsWin(e.rec)) / with.Count;
                double rateWithout = (double)without.Count(e => IsWin(e.rec)) / without.Count;
                sum += w * (rateWith - rateWithout);
                weight += w;
            }
            double diff = weight > 0 ? sum / weight : 0;
            int appearances = runs.SelectMany(r => r.stages).Sum(x => x.roster != null ? x.roster.Count(v => v == name) : 0);
            int kills = runs.SelectMany(r => r.stages).Sum(x => x.kills != null ? x.kills.Count(v => v == name) : 0);
            int lost = runs.SelectMany(r => r.stages).Sum(x => x.losses != null ? x.losses.Count(v => v == name) : 0);
            string killText = appearances > 0 ? ((double)kills / appearances).ToString("0.00") : "-";
            string lostText = appearances > 0 ? Pct(lost, appearances) : "-";
            string diffText = weight > 0 ? (diff >= 0 ? "+" : "") + (diff * 100).ToString("0.0") + "pt" : "-";
            rows.Add(new KeyValuePair<double, string>(diff, "| " + name + " | " + offered + " | " + chosen
                + " | " + diffText + " | " + killText + " | " + lostText + " |"));
        }
        foreach (var row in rows.OrderByDescending(r => r.Key)) sb.AppendLine(row.Value);
        sb.AppendLine();
        // ---- 敵を倒した駒 ----
        sb.AppendLine("## 敵を倒した駒（全周の合計）");
        sb.AppendLine();
        var killers = runs.SelectMany(r => r.stages).SelectMany(x => x.kills ?? new string[0])
            .GroupBy(v => v).OrderByDescending(g => g.Count()).ToList();
        int totalKills = killers.Sum(g => g.Count());
        foreach (var g in killers.Take(15))
            sb.AppendLine("- " + g.Key + ": " + g.Count() + "（" + Pct(g.Count(), totalKills) + "）");
        sb.AppendLine();

        // ---- 気になる点 ----
        sb.AppendLine("## 気になる点");
        sb.AppendLine();
        bool any = false;
        for (int s = 1; s <= maxStage; s++)
        {
            var recs = runs.SelectMany(r => r.stages).Where(x => x.stage == s).ToList();
            if (recs.Count < 10) continue;
            double rate = (double)recs.Count(IsWin) / recs.Count;
            double judged = (double)recs.Count(x => x.outcome.StartsWith("Judged") || x.outcome == "Timeout") / recs.Count;
            if (rate < 0.6) { sb.AppendLine("- 第" + s + "局の勝率が低い（" + Pct(rate) + "）"); any = true; }
            if (s > 1 && rate > 0.98) { sb.AppendLine("- 第" + s + "局はほぼ負けない（" + Pct(rate) + "）"); any = true; }
            if (judged > 0.2) { sb.AppendLine("- 第" + s + "局は手数切れの判定が多い（" + Pct(judged) + "）"); any = true; }
        }
        if (!any) sb.AppendLine("- 特になし");
        return sb.ToString();
    }

    private static bool IsWin(SimStageRecord x) { return x.outcome == "Won" || x.outcome == "JudgedWin"; }
    private static string Pct(int a, int b) { return b > 0 ? Pct((double)a / b) : "-"; }
    private static string Pct(double v) { return (v * 100).ToString("0.0") + "%"; }
}
