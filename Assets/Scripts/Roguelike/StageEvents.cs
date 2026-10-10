using System.Collections.Generic;
using UnityEngine;

/// <summary>局の合間のできごと（勝った局と次の局の間に、2つから1つを選ぶ）</summary>
public enum StageEventKind
{
    Rest,       // 部室で休む: C3の体力+2（最後まで）
    Snack,      // 差し入れ: C3以外の全員の体力+1（最後まで）
    Camp,       // 合宿: 仲間の部員全員の練度+1
    Sparring,   // 練習試合: いちばん練度の低い部員の練度+3
    ClubFund,   // 部費: この局の引き直し+2回
    Challenge   // 強敵に挑む: この局の敵は体力+1、そのかわり仲間を2枚選べる
}

public static class StageEvents
{
    /// <summary>この局の前にできごとがあるか（BalanceTuning.EventFromStage から EventEvery 局ごと）</summary>
    public static bool HappensBefore(int stage)
    {
        if (!BalanceTuning.StageEventsOn || stage < BalanceTuning.EventFromStage) return false;
        return (stage - BalanceTuning.EventFromStage) % Mathf.Max(1, BalanceTuning.EventEvery) == 0;
    }

    public static string Title(StageEventKind kind)
    {
        switch (kind)
        {
            case StageEventKind.Rest: return "部室で休む";
            case StageEventKind.Snack: return "差し入れ";
            case StageEventKind.Camp: return "合宿";
            case StageEventKind.Sparring: return "練習試合";
            case StageEventKind.ClubFund: return "部費が下りた";
            case StageEventKind.Challenge: return "強敵に挑む";
            default: return "";
        }
    }

    public static string Description(StageEventKind kind)
    {
        switch (kind)
        {
            case StageEventKind.Rest: return "部室でひと休み。C3の体力が +" + BalanceTuning.EventRestC3HP + " される（最後まで続く）。";
            case StageEventKind.Snack: return "誰かが差し入れを持ってきた。C3以外の味方全員の体力が +" + BalanceTuning.EventSnackHP + " される（最後まで続く）。";
            case StageEventKind.Camp: return "みんなで合宿。仲間の部員全員の練度が +" + BalanceTuning.EventCampXp + " される。";
            case StageEventKind.Sparring: return "いちばん練度の低い部員が練習試合で鍛えられる。その部員の練度が +" + BalanceTuning.EventSparringXp + " される。";
            case StageEventKind.ClubFund: return "部費で仲間探しがはかどる。この局の仲間選びで、引き直しが" + BalanceTuning.EventFundRerolls + "回増える。";
            case StageEventKind.Challenge: return "あえて強い相手に挑む。この局の敵は" + ChallengePenalty() + "、そのかわり仲間を" + (1 + BalanceTuning.EventChallengePicks) + "枚選べる。";
            default: return "";
        }
    }

    private static string ChallengePenalty()
    {
        int hp = BalanceTuning.EventChallengeEnemyHp, atk = BalanceTuning.EventChallengeEnemyAtk;
        if (hp > 0 && atk > 0) return "攻撃 +" + atk + "・体力 +" + hp + " されるが";
        if (atk > 0) return "攻撃が +" + atk + " されるが";
        return "体力が +" + hp + " されるが";
    }

    /// <summary>カードの印の文字と色</summary>
    public static string Glyph(StageEventKind kind)
    {
        switch (kind)
        {
            case StageEventKind.Rest: return "休";
            case StageEventKind.Snack: return "食";
            case StageEventKind.Camp: return "合";
            case StageEventKind.Sparring: return "練";
            case StageEventKind.ClubFund: return "費";
            case StageEventKind.Challenge: return "挑";
            default: return "";
        }
    }

    public static Color GlyphColor(StageEventKind kind)
    {
        switch (kind)
        {
            case StageEventKind.Rest: return Palette.HP;
            case StageEventKind.Snack: return Palette.HP;
            case StageEventKind.Camp: return Palette.GoldLight;
            case StageEventKind.Sparring: return Palette.GoldLight;
            case StageEventKind.ClubFund: return Palette.DEF;
            case StageEventKind.Challenge: return Palette.ATK;
            default: return Palette.Gold;
        }
    }

    /// <summary>重ならないように count 個選ぶ（部員がいなければ練度のできごとは出さない）</summary>
    public static List<StageEventKind> Draw(int count, bool hasMembers)
    {
        var pool = new List<StageEventKind>
        {
            StageEventKind.Rest, StageEventKind.Snack, StageEventKind.ClubFund, StageEventKind.Challenge
        };
        if (hasMembers)
        {
            pool.Add(StageEventKind.Camp);
            pool.Add(StageEventKind.Sparring);
        }
        var result = new List<StageEventKind>();
        while (result.Count < count && pool.Count > 0)
        {
            int i = Random.Range(0, pool.Count);
            result.Add(pool[i]);
            pool.RemoveAt(i);
        }
        return result;
    }
}
