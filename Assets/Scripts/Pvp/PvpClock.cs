using UnityEngine;

/// <summary>
/// 対人戦の持ち時間: 1手 BalanceTuning.PvpTurnSeconds 秒。過ぎたら自動でパスし、同じ人が続けて PvpTimeoutsToLose 回時間切れなら負け。
/// 数えるのは、手番の初めの能力が終わって人が動かせるようになってから。オンラインでは自分の手番だけ数える（相手の分は相手のゲームが数える）。
/// </summary>
public class PvpClock : MonoBehaviour
{
    public static PvpClock Instance { get; private set; }

    private int countingMove = -1;
    private Team countingTeam;
    private float startTime;
    private bool started;
    private bool expiredThisTurn;
    private readonly int[] timeouts = new int[2];

    public static void Ensure()
    {
        if (Instance != null) { Instance.ResetAll(); return; }
        var obj = new GameObject("PvpClock");
        Instance = obj.AddComponent<PvpClock>();
    }

    private void ResetAll()
    {
        countingMove = -1;
        started = false;
        expiredThisTurn = false;
        timeouts[0] = timeouts[1] = 0;
    }

    /// <summary>この手番の残り秒（数えていなければ -1）</summary>
    public int Remaining
    {
        get
        {
            if (!started || BalanceTuning.PvpTurnSeconds <= 0) return -1;
            return Mathf.Max(0, Mathf.CeilToInt(BalanceTuning.PvpTurnSeconds - (Time.time - startTime)));
        }
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        if (!PvpMatch.Active || gm == null || BalanceTuning.PvpTurnSeconds <= 0 || gm.currentPhase != GamePhase.Battle) { started = false; return; }

        // 手番が変わったら数え直す（前の手番が時間切れでなければ、その人の連続の時間切れは0に戻す）
        if (gm.MoveCount != countingMove)
        {
            if (countingMove >= 0 && !expiredThisTurn) timeouts[(int)countingTeam] = 0;
            countingMove = gm.MoveCount;
            countingTeam = gm.currentTurn;
            started = false;
            expiredThisTurn = false;
        }
        if (!started)
        {
            if (gm.IsTurnProcessing) return;   // 手番の初めの能力が終わるまでは数えない
            started = true;
            startTime = Time.time;
        }
        if (expiredThisTurn || !gm.IsHumanTeam(gm.currentTurn)) return;   // 相手のゲームが数える
        if (Time.time - startTime < BalanceTuning.PvpTurnSeconds) return;
        if (gm.IsTurnProcessing || (InputManager.Instance != null && InputManager.Instance.IsBusy)) return;

        expiredThisTurn = true;
        Team team = gm.currentTurn;
        timeouts[(int)team]++;
        if (InputManager.Instance != null) InputManager.Instance.ClearSelection();
        if (timeouts[(int)team] >= BalanceTuning.PvpTimeoutsToLose)
        {
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(PvpMatch.WhoName(team), team) + " は" + BalanceTuning.PvpTimeoutsToLose + "回続けて時間切れになった");
            gm.Resign();
            return;
        }
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(PvpMatch.WhoName(team), team) + " は時間切れ（パス）。続けて"
                + BalanceTuning.PvpTimeoutsToLose + "回で負け");
        if (PvpMatch.Online && PvpOnline.Instance != null) PvpOnline.Instance.SendPass();
        gm.EndTurn();
    }
}
