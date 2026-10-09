public static class KinsokuHelper
{
    // 行頭禁則文字（行頭に来てはいけない文字）
    private static readonly string NoStartChars = "\u3002\u3001\uFF0E\uFF0C\uFF01\uFF1F\uFF09\u3011\u300D\u300F\u300B\u3009\uFF5D}!?.,;:\u2026\u30FB\u2015\u2014\u2019\u201D\u00BB\u3015\u3017";

    // 行末禁則文字（行末に来てはいけない文字）
    private static readonly string NoEndChars = "\uFF08\u3010\u300C\u300E\u300A\u3008\uFF5B{\u2018\u201C\u00AB\u3014\u3016";

    private const char WordJoiner = '\u2060';

    public static string Apply(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var sb = new System.Text.StringBuilder(text.Length + text.Length / 2);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            // 行頭禁則: この文字の前に改行させない
            if (NoStartChars.IndexOf(c) >= 0 && i > 0)
                sb.Append(WordJoiner);

            sb.Append(c);

            // 行末禁則: この文字の後に改行させない
            if (NoEndChars.IndexOf(c) >= 0 && i < text.Length - 1)
                sb.Append(WordJoiner);
        }
        return sb.ToString();
    }
}
