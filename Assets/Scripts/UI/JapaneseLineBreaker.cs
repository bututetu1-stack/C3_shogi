using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 日本語の改行位置を決める。
/// ・行頭禁則（、。」ー ゃ など）と行末禁則（「（ など）を守る
/// ・できるだけ文節の切れ目（句読点の後、ひらがな→漢字/カタカナ の境目など）で改行する
/// ・英数字のまとまり（C3、1マス、+2 など）は途中で切らない
/// リッチテキストのタグ（&lt;color=...&gt;）は幅ゼロとして扱い、分割しない。
/// </summary>
public static class JapaneseLineBreaker
{
    // 行頭に来てはいけない文字
    private const string NoLineStart =
        "、。，．,.・：；:;？！?!ー－―‐〜～…‥）)」』】〕〉》］]｝}〙〗’”%％" +
        "ぁぃぅぇぉっゃゅょゎゕゖァィゥェォッャュョヮヵヶㇰㇱㇲㇳㇴㇵㇶㇷㇸㇹㇺㇻㇼㇽㇾㇿ々ゝゞヽヾ";
    // 行末に来てはいけない文字
    private const string NoLineEnd = "（(「『【〔〈《［[｛{〘〖‘“";
    // この文字の後は改行してよい（句読点・閉じ括弧）
    private const string BreakAfter = "、。，．！？!?」』）)】〕〉》］｝…";

    private enum CharClass { Kanji, Hiragana, Katakana, Latin, Space, Other }

    private static CharClass Classify(char c)
    {
        if (c == ' ' || c == '　' || c == '\t') return CharClass.Space;
        if (c >= 'ぁ' && c <= 'ゟ') return CharClass.Hiragana;
        if ((c >= '゠' && c <= 'ヿ') || (c >= 'ｦ' && c <= 'ﾟ') || (c >= 'ㇰ' && c <= 'ㇿ')) return CharClass.Katakana;
        if ((c >= '一' && c <= '鿿') || (c >= '㐀' && c <= '䶿') || c == '々' || c == '〆' || c == '〇') return CharClass.Kanji;
        if (c < 0x80 || (c >= '０' && c <= 'ｚ') || c == '∞' || c == '×') return CharClass.Latin;
        return CharClass.Other;
    }

    /// <summary>prev と next の間が文節の切れ目（改行に適した位置）か</summary>
    private static bool IsPhraseBoundary(char prev, char next)
    {
        if (NoLineStart.IndexOf(next) >= 0) return false;
        if (NoLineEnd.IndexOf(prev) >= 0) return false;
        if (BreakAfter.IndexOf(prev) >= 0) return true;
        if (NoLineEnd.IndexOf(next) >= 0) return true;

        CharClass a = Classify(prev);
        CharClass b = Classify(next);
        if (a == CharClass.Space) return true;
        if (b == CharClass.Space) return false;
        if (a == b) return false;
        // 英数字の後ろに日本語が続くときは切らない（「C3以外」「1マス」「2ダメージ」）。
        // 漢字・カナの直後の英数字もくっつける（「体力+2」）。助詞の後なら切ってよい（「に｜1マス」）
        if (b == CharClass.Latin) return a == CharClass.Hiragana;
        if (a == CharClass.Latin) return false;
        // ひらがな（助詞・活用語尾）の後に漢字・カタカナが来たら新しい文節
        if (a == CharClass.Hiragana) return true;
        // 漢字・カタカナの後のひらがなは送り仮名・助詞なのでくっつける
        if (b == CharClass.Hiragana) return false;
        // 漢字とカタカナが続くのは複合語（「毎ターン開始時」「敵ターン」）なので切らない
        bool aWord = a == CharClass.Kanji || a == CharClass.Katakana;
        bool bWord = b == CharClass.Kanji || b == CharClass.Katakana;
        if (aWord && bWord) return false;
        return true;
    }

    // ひらがなで終わるのに切ってはいけないよく出る言葉（「いつの｜間にか」を防ぐ）
    private static readonly string[] JoinedWords = { "いつの間", "その他", "その場", "この世", "あの世" };

    /// <summary>i の位置が「切ってはいけない言葉」の内側か</summary>
    private static bool InsideJoinedWord(string text, int i)
    {
        foreach (string w in JoinedWords)
        {
            for (int k = 1; k < w.Length; k++)
            {
                int start = i - k;
                if (start >= 0 && start + w.Length <= text.Length && string.CompareOrdinal(text, start, w, 0, w.Length) == 0)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 文節に収まらない長い語を文字単位で切るときの優先度（小さいほど自然。0は禁則で不可）。
    /// 1: ひらがなの途中 / 2: 漢字の熟語の途中 / 3: 送り仮名の前 / 4: カタカナ語の途中など / 5: 英数字の途中
    /// </summary>
    private static int BreakPriority(char prev, char next)
    {
        if (NoLineStart.IndexOf(next) >= 0) return 0;
        if (NoLineEnd.IndexOf(prev) >= 0) return 0;
        CharClass a = Classify(prev);
        CharClass b = Classify(next);
        if (a == CharClass.Hiragana && b == CharClass.Hiragana) return 1;
        if (a == CharClass.Kanji && b == CharClass.Kanji) return 2;
        if ((a == CharClass.Kanji || a == CharClass.Katakana) && b == CharClass.Hiragana) return 3;
        if (a == CharClass.Latin && b == CharClass.Latin) return 5;
        return 4;
    }

    /// <summary>
    /// text を maxWidth に収まるように改行する。measure は1行の文字列の描画幅を返す関数
    /// </summary>
    public static string Wrap(string text, float maxWidth, Func<string, float> measure)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0f) return text;

        var result = new StringBuilder(text.Length + 16);
        string[] paragraphs = text.Replace("\r\n", "\n").Split('\n');
        for (int p = 0; p < paragraphs.Length; p++)
        {
            if (p > 0) result.Append('\n');
            WrapParagraph(paragraphs[p], maxWidth, measure, result);
        }
        return result.ToString();
    }

    private static void WrapParagraph(string text, float maxWidth, Func<string, float> measure, StringBuilder output)
    {
        List<string> segments = SplitPhrases(text);
        var line = new StringBuilder();
        bool firstLine = true;

        foreach (string segment in segments)
        {
            if (measure(line.ToString() + segment) <= maxWidth)
            {
                line.Append(segment);
                continue;
            }

            // 今の行を確定して、文節を次の行へ
            if (line.Length > 0)
            {
                EmitLine(line.ToString(), output, ref firstLine);
                line.Length = 0;
            }

            // 1つの文節が行幅を超える場合は、自然な位置を選んで文字単位で分ける
            string rest = TrimStartSpaces(segment);
            while (rest.Length > 0 && measure(rest) > maxWidth)
            {
                int cut = FindCharBreak(rest, maxWidth, measure);
                EmitLine(rest.Substring(0, cut), output, ref firstLine);
                rest = TrimStartSpaces(rest.Substring(cut));
            }
            line.Append(rest);
        }

        if (line.Length > 0 || firstLine)
            EmitLine(line.ToString(), output, ref firstLine);
    }
    private static void EmitLine(string line, StringBuilder output, ref bool firstLine)
    {
        if (!firstLine) output.Append('\n');
        output.Append(line.TrimEnd(' ', '　'));
        firstLine = false;
    }

    private static string TrimStartSpaces(string s)
    {
        return s.TrimStart(' ', '　');
    }

    /// <summary>行幅に収まる位置のうち、なるべく自然な切れ目（禁則を守る）を探す</summary>
    private static int FindCharBreak(string s, float maxWidth, Func<string, float> measure)
    {
        // 優先度ごとに「収まる範囲で一番後ろの位置」を記録しておく
        var bestByPriority = new int[6];
        char prevVisible = '\0';
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '<')
            {
                int close = s.IndexOf('>', i);
                if (close > i) { i = close; continue; }
            }
            if (prevVisible != '\0')
            {
                int priority = BreakPriority(prevVisible, s[i]);
                if (priority > 0)
                {
                    if (measure(s.Substring(0, i)) > maxWidth) break;
                    bestByPriority[priority] = i;
                }
            }
            prevVisible = s[i];
        }

        // 自然な切れ目（1〜3）を優先し、なければ順に妥協する
        int best = Math.Max(bestByPriority[1], Math.Max(bestByPriority[2], bestByPriority[3]));
        if (best <= 0) best = bestByPriority[4];
        if (best <= 0) best = bestByPriority[5];
        if (best > 0) return best;
        // どうしても収まらない場合は最初の1文字（直前のタグを含む）だけ進める
        int idx = 0;
        while (idx < s.Length && s[idx] == '<')
        {
            int close = s.IndexOf('>', idx);
            if (close < 0) break;
            idx = close + 1;
        }
        return Math.Min(s.Length, idx + 1);
    }

    /// <summary>文節（改行してよい単位）に分ける。タグは直後の文字と同じ文節に入れる</summary>
    private static List<string> SplitPhrases(string text)
    {
        var segments = new List<string>();
        var current = new StringBuilder();
        char prevVisible = '\0';

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '<')
            {
                int close = text.IndexOf('>', i);
                if (close > i)
                {
                    string tag = text.Substring(i, close - i + 1);
                    bool closing = tag.StartsWith("</");
                    // 閉じタグは前の文節、開きタグは次の文字の文節へ
                    if (!closing && prevVisible != '\0' && i + tag.Length < text.Length)
                    {
                        char next = NextVisible(text, close + 1);
                        if (next != '\0' && IsPhraseBoundary(prevVisible, next) && current.Length > 0)
                        {
                            segments.Add(current.ToString());
                            current.Length = 0;
                            prevVisible = '\0';
                        }
                    }
                    current.Append(tag);
                    i = close;
                    continue;
                }
            }

            if (prevVisible != '\0' && current.Length > 0 && IsPhraseBoundary(prevVisible, c) && !InsideJoinedWord(text, i))
            {
                segments.Add(current.ToString());
                current.Length = 0;
            }
            current.Append(c);
            prevVisible = c;
        }
        if (current.Length > 0) segments.Add(current.ToString());
        return segments;
    }

    private static char NextVisible(string text, int start)
    {
        for (int i = start; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                int close = text.IndexOf('>', i);
                if (close > i) { i = close; continue; }
            }
            return text[i];
        }
        return '\0';
    }
}
