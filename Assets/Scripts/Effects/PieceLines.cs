using UnityEngine;

/// <summary>
/// 駒のセリフ（吹き出しに出す）。説明文の小ネタから拾っている。
/// 好きに書き換えてよい（配列は候補からランダムに1つ選ぶ）
/// </summary>
public static class PieceLines
{
    // 僕: 周りの駒が強くなったとき
    public static readonly string[] BokuCheer = { "成ったな……" };
    public static readonly string[] BokuCheerPromoted = { "（後方腕組）", "成ったな……" };

    // ヲツ: 中華を作ったとき
    public static readonly string[] WotsuCook = { "作れますよ" };

    // 門人: 自動で突き進むとき／成ってから絶起したとき
    public static readonly string[] MoninRush = { "（怒涛の罵倒）", "まくし立てる！" };
    public static readonly string[] MoninOversleep = { "zzz……（絶起）" };

    // なこ: ドパに向かって突っ走るとき
    public static readonly string[] NakoDash = { "ドパ！", "ドーパミン！" };

    // 李白: 駒を裏返したとき
    public static readonly string[] RihakuFlip = { "嘘です", "本当です", "……たぶん" };

    // SN: 動きすぎて力尽きたとき
    public static readonly string[] SnExhausted = { "過労……" };

    // 小錦: 攻撃されたとき（ときどき）
    public static readonly string[] KonishikiBullied = { "またいじめられた……" };

    // 物鉄: 成る前に倒されたとき
    public static readonly string[] MonotetsuDown = { "南無三" };

    // けい・異端: 周りの敵の数値を下げるとき
    public static readonly string[] KeiBug = { "それはバグやろ！" };
    public static readonly string[] ItanBug = { "バグ多すぎやろ！" };

    // ユウ: 攻撃するとき（ときどき）
    public static readonly string[] YuuAttack = { "お前やりませんねスギぃ！", "おい、創作しろよ" };

    // きぷ・へる: 冷笑するとき／きぷが敵を倒したとき
    public static readonly string[] KipuLaugh = { "うおw", "ええてw", "どわーw", "クカw", "かっけーw", "ちょw" };
    public static readonly string[] KipuTaunt = { "えwwこの武器持ってる人に負けるんですかww" };

    // 敵
    public static readonly string[] GundaishouRally = { "かかれ！" };
    public static readonly string[] YomigaeruSummon = { "黄泉より来たれ" };

    /// <summary>カットインの文言と色</summary>
    public class CutIn
    {
        public string title;
        public string subtitle;
        public Color band;
        public Color accent;

        public CutIn(string title, string subtitle, int band, int accent)
        {
            this.title = title;
            this.subtitle = subtitle;
            this.band = Palette.Hex(band);
            this.accent = Palette.Hex(accent);
        }
    }

    /// <summary>部員が成るときのカットイン（物鉄は「提督 着任」が別にある。なければ null）</summary>
    public static CutIn PromotionCutIn(PieceType type)
    {
        switch (type)
        {
            case PieceType.Boku: return new CutIn("僕 覚醒", "（後方腕組）成ったな……", 0x24402F, 0xD4AF5F);
            case PieceType.Wotsu: return new CutIn("中華鍋 装備", "殺せるようになってしまった", 0x6E1A12, 0xF0B04A);
            case PieceType.Monin: return new CutIn("門人 覚醒", "ものすごい勢いで動く。ただし絶起する", 0x3A0C0C, 0xE0503C);
            case PieceType.Nako: return new CutIn("なこ 暴走", "さらなるドパを求めて", 0x5E1F4A, 0xFF8AC8);
            case PieceType.Rihaku: return new CutIn("李白 成る", "相変わらず趣味が悪い", 0x2E2250, 0xB894FF);
            case PieceType.SN: return new CutIn("SN 過労", "過労により死亡した", 0x2A2A2E, 0x9AA0AA);
            case PieceType.Konishiki: return new CutIn("小錦 風邪", "風邪をひいて力尽きた……", 0x1E3550, 0x9FD4FF);
            case PieceType.Kei: return new CutIn("けい 異端化", "倫理観と人の心とその他諸々を失った", 0x0B2618, 0x5CFF9A);
            case PieceType.Yuu: return new CutIn("ユウ 加速", "防御力とさらなる機動力を手に入れた", 0x3A1608, 0xFF9A3C);
            case PieceType.Kipu: return new CutIn("へる 降臨", "冷笑を極めた（25連冷笑コンボ）", 0x14202E, 0x9ED8FF);
            default: return null;
        }
    }

    /// <summary>ボスが初めて出る局の登場カットイン（なければ null）</summary>
    public static CutIn BossCutIn(int stage, out PieceType boss)
    {
        switch (stage)
        {
            case 10: boss = PieceType.Raitei; return new CutIn("雷帝 降臨", "雷を操る皇帝", 0x2A2208, 0xFFD54A);
            case 12: boss = PieceType.Ryuujin; return new CutIn("龍神 覚醒", "古代の龍神が目を覚ます", 0x0C2A36, 0x66D9FF);
            case 15: boss = PieceType.Maou; return new CutIn("魔王 降臨", "全てを統べる魔界の王", 0x1A0A26, 0xB066FF);
            default: boss = PieceType.Pawn; return null;
        }
    }

    /// <summary>成ったときのひとこと（なければ null）</summary>
    public static string[] OnPromote(PieceType type)
    {
        switch (type)
        {
            case PieceType.Boku: return new[] { "（後方腕組）" };
            case PieceType.Wotsu: return new[] { "中華鍋、装備！" };
            case PieceType.Monin: return new[] { "覚醒！" };
            case PieceType.Nako: return new[] { "もっとドパを……" };
            case PieceType.SN: return new[] { "（過労）" };
            case PieceType.Konishiki: return new[] { "ハックション！" };
            case PieceType.Kei: return new[] { "（人の心を失った）" };
            case PieceType.Yuu: return new[] { "おい、創作しろよ" };
            case PieceType.Kipu: return new[] { "クカw" };
            default: return null;
        }
    }
}
