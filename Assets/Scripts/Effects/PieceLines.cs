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

    // 敵
    public static readonly string[] GundaishouRally = { "かかれ！" };
    public static readonly string[] YomigaeruSummon = { "黄泉より来たれ" };

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
            default: return null;
        }
    }
}
