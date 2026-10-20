using MkTeX.Fonts;
using MkTeX.Rendering;

namespace MkTeX.Tests;

public class LayoutTests
{
    private const double Size = 10;
    private static readonly MathFont Font = MathFont.Default;

    private static MathLayout L(string source, bool display = false) =>
        MathEngine.Layout(source, new MathOptions { FontSize = Size, Display = display });

    private static List<GlyphItem> Glyphs(MathLayout layout) => layout.Items.OfType<GlyphItem>().ToList();

    private static GlyphItem GlyphFor(MathLayout layout, int codePoint)
    {
        var glyph = Font.GlyphIndex(codePoint);
        return Glyphs(layout).First(g => g.Glyph == glyph);
    }

    private static double Em(MathConstant constant) => Font.Constant(constant) * Size;

    [Fact]
    public void BaselineIsAtHeight()
    {
        var layout = L("x");
        var x = Assert.Single(Glyphs(layout));
        Assert.Equal(layout.Height, x.Y, 6);
        Assert.Equal(0, x.X, 6);
        Assert.Equal(Font.Advance(Font.GlyphIndex(0x1D465)) * Size, layout.Width, 6);
        Assert.Null(layout.Error);
    }

    [Fact]
    public void SuperscriptIsRaisedAndScaled()
    {
        var layout = L("x^2");
        var two = GlyphFor(layout, '2');
        var x = GlyphFor(layout, 0x1D465);
        Assert.Equal(Size * 0.7, two.Size, 6);
        Assert.True(x.Y - two.Y >= Em(MathConstant.SuperscriptShiftUp) - 1e-6);
    }

    [Fact]
    public void ScriptsKeepTheMinimumGap()
    {
        var layout = L("x_{\\frac{a}{b}}^{\\frac{c}{d}}");
        Assert.Null(layout.Error);
        var c = GlyphFor(layout, 0x1D450);
        var b = GlyphFor(layout, 0x1D44F);
        Assert.True(b.Y > c.Y);
    }

    [Fact]
    public void ScriptScriptStyleUsesSecondScale()
    {
        var layout = L("x^{y^z}");
        Assert.Equal(Size * 0.55, GlyphFor(layout, 0x1D467).Size, 6);
    }

    [Fact]
    public void BinaryAndRelationSpacingFollowsTeX()
    {
        var mu = Size / 18;
        var plus = L("a+b");
        var aWidth = Font.Advance(Font.GlyphIndex(0x1D44E)) * Size;
        Assert.Equal(aWidth + 4 * mu, GlyphFor(plus, '+').X, 6);

        var equals = L("a=b");
        Assert.Equal(aWidth + 5 * mu, GlyphFor(equals, '=').X, 6);

        var unary = L("-b");
        Assert.Equal(0, GlyphFor(unary, 0x2212).X, 6);

        var afterRel = L("a=-b");
        var minus = GlyphFor(afterRel, 0x2212);
        var b = GlyphFor(afterRel, 0x1D44F);
        Assert.Equal(minus.X + Font.Advance(Font.GlyphIndex(0x2212)) * Size, b.X, 6);
    }

    [Fact]
    public void ConditionalSpacesDisappearInScripts()
    {
        var script = L("x^{a+b}");
        var plus = GlyphFor(script, '+');
        var a = GlyphFor(script, 0x1D44E);
        Assert.Equal(a.X + Font.Advance(Font.GlyphIndex(0x1D44E)) * Size * 0.7, plus.X, 6);
    }

    [Fact]
    public void FractionStacksAroundTheAxis()
    {
        var layout = L("\\frac{a}{b}", display: true);
        var rule = Assert.Single(layout.Items.OfType<RuleItem>());
        var a = GlyphFor(layout, 0x1D44E);
        var b = GlyphFor(layout, 0x1D44F);
        var axisY = layout.Height - Em(MathConstant.AxisHeight);
        Assert.Equal(axisY, rule.Y + rule.Height / 2, 6);
        Assert.Equal(Em(MathConstant.FractionRuleThickness), rule.Height, 6);
        Assert.True(a.Y < rule.Y);
        Assert.True(b.Y > rule.Y + rule.Height);
        Assert.Equal(Size, a.Size, 6);
        Assert.Equal(Size * 0.7, GlyphFor(L("\\frac{a}{b}"), 0x1D44E).Size, 6);
    }

    [Fact]
    public void DisplayOperatorsAreLarger()
    {
        var inline = L("\\sum");
        var display = L("\\sum", display: true);
        Assert.True(display.TotalHeight > inline.TotalHeight);
        var variants = Font.Vertical(Font.GlyphIndex(0x2211))!.Variants;
        var expected = variants.FirstOrDefault(v => v.Advance >= Font.Constant(MathConstant.DisplayOperatorMinHeight)) ?? variants[^1];
        Assert.Equal(expected.Glyph, Assert.Single(Glyphs(display)).Glyph);
    }

    [Fact]
    public void DisplayLimitsGoAboveAndBelow()
    {
        var layout = L("\\sum_{i=1}^{n} x", display: true);
        var sum = Glyphs(layout).First(g => g.Size == Size && Font.Vertical(Font.GlyphIndex(0x2211))!.Variants.Any(v => v.Glyph == g.Glyph));
        var n = GlyphFor(layout, 0x1D45B);
        var i = GlyphFor(layout, 0x1D456);
        Assert.True(n.Y < sum.Y - 1);
        Assert.True(i.Y > sum.Y);
        Assert.True(n.X > sum.X && n.X < sum.X + Font.Advance(sum.Glyph) * Size);

        var inline = L("\\sum_{i=1}^{n} x");
        var inlineN = GlyphFor(inline, 0x1D45B);
        Assert.True(inlineN.X > GlyphFor(inline, 0x2211).X + Font.Advance(Font.GlyphIndex(0x2211)) * Size - 1e-6);
    }

    [Fact]
    public void DelimitersGrowWithContent()
    {
        var small = L("\\left( x \\right)");
        var tall = L("\\left( \\frac{\\frac{a}{b}}{\\frac{c}{d}} \\right)", display: true);
        var smallParen = Glyphs(small)[0];
        var tallParens = Glyphs(tall).Where(g => g.X < 1).ToList();
        Assert.Equal(Font.GlyphIndex('('), smallParen.Glyph);
        Assert.True(tall.TotalHeight > small.TotalHeight * 2);
        Assert.True(tallParens.Count > 1 || tallParens[0].Glyph != Font.GlyphIndex('('));
    }

    [Fact]
    public void VeryTallDelimitersUseAssemblies()
    {
        var rows = string.Join(" \\\\ ", Enumerable.Range(0, 12).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var layout = L($"\\left( \\begin{{matrix}} {rows} \\end{{matrix}} \\right)");
        var parts = Font.Vertical(Font.GlyphIndex('('))!.Assembly.Select(p => p.Glyph).ToHashSet();
        Assert.True(Glyphs(layout).Count(g => parts.Contains(g.Glyph)) >= 3);
    }

    [Fact]
    public void RadicalRuleCoversTheBody()
    {
        var layout = L("\\sqrt{x+y}");
        var rule = Assert.Single(layout.Items.OfType<RuleItem>());
        var x = GlyphFor(layout, 0x1D465);
        var y = GlyphFor(layout, 0x1D466);
        Assert.True(rule.X <= x.X + 1e-6);
        Assert.True(rule.X + rule.Width >= y.X + Font.Advance(Font.GlyphIndex(0x1D466)) * Size - 1e-6);
        Assert.True(rule.Y < x.Y - Font.Bounds(Font.GlyphIndex(0x1D465)).YMax * Size);
    }

    [Fact]
    public void RadicalDegreeIsScriptScript()
    {
        var layout = L("\\sqrt[3]{x}");
        Assert.Equal(Size * 0.55, GlyphFor(layout, '3').Size, 6);
    }

    [Fact]
    public void AccentsSitAboveTheBase()
    {
        var layout = L("\\hat{A}");
        var hat = GlyphFor(layout, 0x302);
        var a = GlyphFor(layout, 0x1D434);
        Assert.True(hat.Y < a.Y);
        var wide = L("\\widehat{xyzw}");
        Assert.NotEqual(Font.GlyphIndex(0x302), Glyphs(wide).OrderBy(g => g.Y).First().Glyph);
    }

    [Fact]
    public void MatricesAlignColumns()
    {
        var layout = L("\\begin{matrix} 1 & 22 \\\\ 333 & 4 \\end{matrix}");
        var four = GlyphFor(layout, '4');
        var two = Glyphs(layout).Where(g => g.Glyph == Font.GlyphIndex('2')).Min(g => g.X);
        Assert.True(four.X > two);
        var first = Glyphs(layout).Where(g => g.Glyph == Font.GlyphIndex('3')).Min(g => g.X);
        Assert.Equal(0, first, 6);
    }

    [Fact]
    public void TagIsLaidOutSeparately()
    {
        var layout = L("E = mc^2 \\tag{3.1}", display: true);
        Assert.NotNull(layout.Tag);
        Assert.Equal(Font.GlyphIndex('('), Glyphs(layout.Tag)[0].Glyph);
        Assert.Null(L("x \\tag*{a}").Tag!.Items.OfType<GlyphItem>().FirstOrDefault(g => g.Glyph == Font.GlyphIndex('(')));
    }

    [Fact]
    public void ColorsPropagateToItems()
    {
        var layout = L("\\color{red} x + \\textcolor{#00f}{y}");
        Assert.Equal("#FF0000", GlyphFor(layout, 0x1D465).Color);
        Assert.Equal("#0000FF", GlyphFor(layout, 0x1D466).Color);
        var defaultColor = MathEngine.Layout("x", new MathOptions { Color = "#123456" });
        Assert.Equal("#123456", Glyphs(defaultColor)[0].Color);
    }

    [Fact]
    public void ErrorsProduceAVisibleFallback()
    {
        var layout = L("\\frac{1}{");
        Assert.NotNull(layout.Error);
        Assert.Equal(MathErrorCode.UnbalancedGroup, layout.Error.Code);
        var text = Assert.Single(layout.Items.OfType<TextItem>());
        Assert.Equal("\\frac{1}{", text.Text);
        Assert.True(layout.Width > 0);
        Assert.NotNull(MathEngine.Validate("\\frac{1}{"));
        Assert.Null(MathEngine.Validate("\\frac{1}{2}"));
    }

    [Fact]
    public void MissingGlyphsFallBackToText()
    {
        var layout = L("\\text{日本}");
        Assert.Equal("日本", Assert.Single(layout.Items.OfType<TextItem>()).Text);
    }

    [Fact]
    public void LineBreaksStackRows()
    {
        var layout = L("a \\\\ b", display: true);
        Assert.True(GlyphFor(layout, 0x1D44F).Y > GlyphFor(layout, 0x1D44E).Y + Size);
    }

    [Fact]
    public void BoxesAndCancelProduceShapes()
    {
        Assert.Single(L("\\boxed{x}").Items.OfType<FrameItem>());
        Assert.Equal("#FFFF00", Assert.Single(L("\\colorbox{yellow}{x}").Items.OfType<FrameItem>()).Fill);
        Assert.Equal(2, L("\\xcancel{x}").Items.OfType<LineItem>().Count());
    }

    [Fact]
    public void LayoutIsDeterministic()
    {
        const string source = "\\int_0^1 \\frac{\\sqrt{x}}{1+x^2}\\,dx";
        Assert.Equal(MathEngine.ToSvg(source), MathEngine.ToSvg(source));
    }

    [Fact]
    public void SvgUsesDefsAndBaselineAlignment()
    {
        var svg = MathEngine.ToSvg("x^2", new MathOptions { FontSize = 20 }, new SvgOptions { IdPrefix = "eq1-", Title = "x^2 <b>" });
        Assert.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\"", svg, StringComparison.Ordinal);
        Assert.Contains("<defs><path id=\"eq1-g", svg, StringComparison.Ordinal);
        Assert.Contains("href=\"#eq1-g", svg, StringComparison.Ordinal);
        Assert.Contains("vertical-align:-", svg, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"x^2 &lt;b&gt;\"", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"currentColor\"", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void SvgIdPrefixIsSanitized()
    {
        var svg = MathEngine.ToSvg("x", svg: new SvgOptions { IdPrefix = "a\"><script>" });
        Assert.DoesNotContain("<script>", svg, StringComparison.Ordinal);
    }
}
