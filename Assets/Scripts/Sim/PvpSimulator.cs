using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>対人戦の自動プレイ1局ぶんの記録</summary>
[Serializable]
public class PvpSimRecord
{
    public int seed;
    public string rule;            // Cost / Random
    public string winner;          // First（先手）/ Second（後手）/ Draw
    public string reason;          // C3撃破 / 判定 / 時間切れ（手数の上限を超えても決まらない）
    public int moves;
    public string[] firstPieces, secondPieces;
    public string[] firstUpgrades, secondUpgrades;
    public int firstCost, secondCost;
    public string[] fleet;         // 艦隊の出来事
    public float seconds;
}

/// <summary>
/// 対人戦の自動プレイ: 2人とも AI（同じ設定）が指す。編成はコスト制ならランダムに持ち点まで、ランダム配布なら同じ候補からランダムに選ぶ。
/// 並べ方は決まった順のまま。先手・後手の勝率や、駒・強化ごとの勝率でコストや決まりを直すのに使う（docs/balance/pvp_stats.js）。
/// </summary>
public static class PvpSimulator
{
    public static int RunBatch(PvpMatch.Rule rule, int runs, int firstSeed, float thinkMs, string jsonlPath)
    {
        if (!BalanceSimulator.CanRun) throw new InvalidOperationException("プレイ中のシーンで実行してください");
        string dir = Path.GetDirectoryName(jsonlPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        int done = 0;
        GameSim.Headless = true;
        try
        {
            for (int i = 0; i < runs; i++)
            {
                PvpSimRecord rec = RunOne(rule, firstSeed + i, thinkMs);
                File.AppendAllText(jsonlPath, JsonUtility.ToJson(rec) + "\n", new UTF8Encoding(false));
                done++;
            }
        }
        finally
        {
            GameSim.Headless = false;
            PvpMatch.End();
            GameSim.BeginStageStats();
        }
        return done;
    }

    public static PvpSimRecord RunOne(PvpMatch.Rule rule, int seed, float thinkMs)
    {
        GameManager gm = GameManager.Instance;
        BoardManager bm = BoardManager.Instance;
        SimpleAI ai = SimpleAI.Instance;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        UnityEngine.Random.InitState(seed);

        PvpMatch.Begin(rule);
        PvpMatch.Seed = seed;
        GameRng.InitState(seed);
        if (rule == PvpMatch.Rule.Cost)
        {
            PvpMatch.RandomCostBuild(PvpMatch.First);
            PvpMatch.RandomCostBuild(PvpMatch.Second);
        }
        else
        {
            var rounds = PvpMatch.BuildRandomRounds();
            foreach (var side in new[] { PvpMatch.First, PvpMatch.Second })
                foreach (var options in rounds)
                {
                    DraftOption pick = options[GameRng.Range(0, options.Count)];
                    if (pick.piece != null) side.pieces.Add(pick.piece);
                    else side.upgrades.Add(pick.upgrade);
                }
        }

        if (BalanceTuning.PvpSimMirror)
        {
            PvpMatch.Second.pieces.Clear();
            PvpMatch.Second.pieces.AddRange(PvpMatch.First.pieces);
            PvpMatch.Second.upgrades.Clear();
            PvpMatch.Second.upgrades.AddRange(PvpMatch.First.upgrades);
        }

        var rec = new PvpSimRecord
        {
            seed = seed,
            rule = rule.ToString(),
            firstPieces = PvpMatch.First.pieces.Select(Name).ToArray(),
            secondPieces = PvpMatch.Second.pieces.Select(Name).ToArray(),
            firstUpgrades = PvpMatch.First.upgrades.Select(u => u.ToString()).ToArray(),
            secondUpgrades = PvpMatch.Second.upgrades.Select(u => u.ToString()).ToArray(),
            firstCost = PvpMatch.First.CostUsed(),
            secondCost = PvpMatch.Second.CostUsed(),
        };

        GameSim.BeginStageStats();
        GameSim.RunSync(gm.SimStartPvp());
        int guard = 0;
        while (gm.currentPhase == GamePhase.Battle && guard++ < GameManager.MoveLimit * 3)
        {
            PieceInstance piece;
            MoveValidator.MoveResult move;
            Team team = gm.currentTurn;
            if (ai.TryChooseMove(team, thinkMs, out piece, out move))
            {
                GameSim.RunSync(CombatResolver.ExecuteMove(piece, move));
                PieceInstance again = gm.TakeBonusMove();
                if (again != null && gm.currentPhase == GamePhase.Battle && ai.TryChooseMove(team, thinkMs, out piece, out move, again))
                    GameSim.RunSync(CombatResolver.ExecuteMove(piece, move));
            }
            if (gm.currentPhase != GamePhase.Battle) break;
            GameSim.RunSync(gm.SimEndTurn());
        }

        rec.winner = gm.PvpWinner.HasValue ? (gm.PvpWinner.Value == Team.Player ? "First" : "Second") : "Draw";
        rec.reason = gm.currentPhase == GamePhase.Battle ? "時間切れ" : gm.PvpEndReason;
        rec.moves = gm.MoveCount;
        rec.fleet = GameSim.StageFleet.ToArray();
        rec.seconds = (float)clock.Elapsed.TotalSeconds;
        return rec;
    }

    private static string Name(PieceData d)
    {
        return d != null ? d.pieceType.ToString() : "";
    }
}
