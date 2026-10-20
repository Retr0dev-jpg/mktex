using System.Text;

namespace MkTeX.Tests;

public class FuzzTests
{
    private static readonly string[] Pieces =
    [
        "x", "1", "+", "-", "=", "(", ")", "[", "]", "|", "'", ",", " ", "{", "}", "^", "_", "&", "\\\\", "#", "$", "~", "%",
        "\\frac", "\\sqrt", "\\sqrt[3]", "\\left(", "\\right)", "\\middle|", "\\left.", "\\right.", "\\alpha", "\\sum", "\\int",
        "\\limits", "\\nolimits", "\\text{a b}", "\\mathbf", "\\mathbb", "\\hat", "\\widehat", "\\overbrace", "\\underbrace",
        "\\begin{matrix}", "\\end{matrix}", "\\begin{cases}", "\\end{cases}", "\\begin{array}{c|l}", "\\end{array}", "\\hline",
        "\\over", "\\choose", "\\color{red}", "\\textcolor{blue}", "\\displaystyle", "\\scriptstyle", "\\big(", "\\Bigg]",
        "\\xrightarrow", "\\xrightarrow[a]", "\\overset", "\\not", "\\boxed", "\\cancel", "\\phantom", "\\rule{1em}{1em}",
        "\\hspace{1em}", "\\kern", "\\,", "\\!", "\\def\\a{x}", "\\a", "\\newcommand{\\b}[1]{#1#1}", "\\b", "\\tag{1}", "\\ce{H2O}",
        "\\operatorname{f}", "\\binom", "\\genfrac", "\\char\"41", "\\unknown", "\\", "\\begin{foo}", "\\end{bar}"
    ];

    [Fact]
    public void RandomInputNeverThrowsAndStaysFinite()
    {
        var random = new Random(20261020);
        for (var i = 0; i < 4000; i++)
        {
            var builder = new StringBuilder();
            var count = random.Next(1, 25);
            for (var j = 0; j < count; j++)
            {
                builder.Append(Pieces[random.Next(Pieces.Length)]);
            }

            var source = builder.ToString();
            var layout = MathEngine.Layout(source, new MathOptions { Display = random.Next(2) == 0 });
            AssertFinite(layout, source);
            _ = Rendering.SvgRenderer.Render(layout);
        }
    }

    [Fact]
    public void RandomCharactersNeverThrow()
    {
        var random = new Random(7);
        for (var i = 0; i < 2000; i++)
        {
            var chars = new char[random.Next(1, 40)];
            for (var j = 0; j < chars.Length; j++)
            {
                chars[j] = (char)random.Next(0x20, 0x7F);
            }

            var source = new string(chars);
            AssertFinite(MathEngine.Layout(source), source);
        }
    }

    [Fact]
    public void LoneSurrogatesAndControlCharactersAreHandled()
    {
        AssertFinite(MathEngine.Layout("x\uD800y\u0000\u001F"), "surrogates");
        AssertFinite(MathEngine.Layout("\\text{\uDC00}"), "text surrogate");
    }

    private static void AssertFinite(MathLayout layout, string source)
    {
        Assert.True(double.IsFinite(layout.Width) && double.IsFinite(layout.Height) && double.IsFinite(layout.Depth), source);
        foreach (var item in layout.Items)
        {
            var finite = item switch
            {
                GlyphItem g => double.IsFinite(g.X) && double.IsFinite(g.Y) && double.IsFinite(g.Size),
                RuleItem r => double.IsFinite(r.X) && double.IsFinite(r.Y) && double.IsFinite(r.Width) && double.IsFinite(r.Height),
                LineItem l => double.IsFinite(l.X1) && double.IsFinite(l.Y1) && double.IsFinite(l.X2) && double.IsFinite(l.Y2),
                FrameItem f => double.IsFinite(f.X) && double.IsFinite(f.Y) && double.IsFinite(f.Width) && double.IsFinite(f.Height),
                TextItem t => double.IsFinite(t.X) && double.IsFinite(t.Y),
                _ => true
            };
            Assert.True(finite, source);
        }
    }
}
