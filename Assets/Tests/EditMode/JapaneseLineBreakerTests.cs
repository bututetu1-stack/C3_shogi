using NUnit.Framework;

public class JapaneseLineBreakerTests
{
    // 全角1文字=1、半角=0.5 として幅を測る（タグは幅ゼロ）
    private static float Measure(string s)
    {
        float w = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '<')
            {
                int close = s.IndexOf('>', i);
                if (close > i) { i = close; continue; }
            }
            w += s[i] < 0x80 ? 0.5f : 1f;
        }
        return w;
    }

    private const string NoLineStart = "、。」』）！？ーっゃゅょ";

    [TestCase("駒をクリックすると、ここに詳しい情報が表示されます。", 8f)]
    [TestCase("敵陣（奥の2〜3段）に入るか、敵陣から出ると、駒は必ず成ります。SNや小錦のように、成ると退場してしまう駒もいるので注意。", 10f)]
    [TestCase("ダメージは「攻撃力 − 防御力」。倒しきれないときは、攻撃した駒はその場に留まります。", 7f)]
    public void Lines_NeverStartWithForbiddenCharacters_AndFitWidth(string text, float width)
    {
        string wrapped = JapaneseLineBreaker.Wrap(text, width, Measure);
        foreach (string line in wrapped.Split('\n'))
        {
            Assert.IsNotEmpty(line);
            Assert.IsTrue(NoLineStart.IndexOf(line[0]) < 0, "行頭禁則違反: 「" + line + "」\n" + wrapped);
            Assert.LessOrEqual(Measure(line), width, "はみ出し: 「" + line + "」");
        }
        Assert.AreEqual(text.Replace(" ", ""), wrapped.Replace("\n", "").Replace(" ", ""), "文字が欠けた・増えた");
    }

    [Test]
    public void BreaksAtPhraseBoundaries_NotInsideWords()
    {
        string wrapped = JapaneseLineBreaker.Wrap("自分の駒を選ぶと動ける場所が表示されます。", 6f, Measure);
        foreach (string line in wrapped.Split('\n'))
        {
            // 「表示」「場所」などの語の途中で切れていない
            Assert.IsFalse(line.EndsWith("表"), wrapped);
            Assert.IsFalse(line.EndsWith("場"), wrapped);
        }
    }

    [Test]
    public void AlphanumericRuns_AreKeptTogether()
    {
        string wrapped = JapaneseLineBreaker.Wrap("この駒はC3以外の全員に1マス先から攻撃できる", 5f, Measure);
        StringAssert.DoesNotContain("C\n3", wrapped);
        StringAssert.DoesNotContain("1\nマス", wrapped);
    }

    [Test]
    public void RichTextTags_AreNotSplit()
    {
        string text = "<color=#66B2FF>歩兵</color> が <color=#FF6666>銀将</color> を撃破！";
        string wrapped = JapaneseLineBreaker.Wrap(text, 4f, Measure);
        foreach (string line in wrapped.Split('\n'))
        {
            Assert.AreEqual(CountChar(line, '<'), CountChar(line, '>'), "タグが途中で切れた: " + wrapped);
        }
    }

    [Test]
    public void ExplicitNewlines_ArePreserved()
    {
        string wrapped = JapaneseLineBreaker.Wrap("一行目。\n二行目。", 20f, Measure);
        Assert.AreEqual("一行目。\n二行目。", wrapped);
    }

    private static int CountChar(string s, char c)
    {
        int n = 0;
        foreach (char ch in s) if (ch == c) n++;
        return n;
    }
}
