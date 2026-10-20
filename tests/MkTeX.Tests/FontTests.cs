using MkTeX.Fonts;

namespace MkTeX.Tests;

public class FontTests
{
    private static readonly MathFont Font = MathFont.Default;

    [Fact]
    public void DefaultFontExposesMathConstants()
    {
        Assert.Equal(1000, Font.UnitsPerEm);
        Assert.True(Font.GlyphCount > 5000);
        Assert.Equal(70, Font.Constant(MathConstant.ScriptPercentScaleDown));
        Assert.Equal(55, Font.Constant(MathConstant.ScriptScriptPercentScaleDown));
        Assert.Equal(0.258, Font.Constant(MathConstant.AxisHeight), 3);
        Assert.Equal(0.068, Font.Constant(MathConstant.FractionRuleThickness), 3);
        Assert.Equal(55, Font.Constant(MathConstant.RadicalDegreeBottomRaisePercent));
        Assert.True(Font.MinConnectorOverlap > 0);
    }

    [Theory]
    [InlineData('x')]
    [InlineData(0x1D465)]
    [InlineData(0x2211)]
    [InlineData(0x221A)]
    [InlineData(0x211D)]
    public void CommonGlyphsHaveOutlines(int codePoint)
    {
        Assert.True(Font.HasGlyph(codePoint));
        var glyph = Font.GlyphIndex(codePoint);
        var outline = Font.Outline(glyph);
        Assert.NotEmpty(outline.Commands);
        Assert.Equal(PathVerb.MoveTo, outline.Commands[0].Verb);
        Assert.Equal(PathVerb.Close, outline.Commands[^1].Verb);
        Assert.True(Font.Advance(glyph) > 0);
        Assert.StartsWith("M", outline.ToSvgPathData(), StringComparison.Ordinal);
    }

    [Fact]
    public void CompositeGlyphsAreResolved()
    {
        var glyph = Font.GlyphIndex(0xC5);
        Assert.NotEqual(0, glyph);
        var outline = Font.Outline(glyph);
        Assert.True(outline.Commands.Count(c => c.Verb == PathVerb.MoveTo) >= 2);
    }

    [Fact]
    public void DelimitersHaveSizeVariantsAndAssemblies()
    {
        var paren = Font.Vertical(Font.GlyphIndex('('));
        Assert.NotNull(paren);
        Assert.True(paren.Variants.Count > 1);
        Assert.Contains(paren.Assembly, p => p.IsExtender);
        for (var i = 1; i < paren.Variants.Count; i++)
        {
            Assert.True(paren.Variants[i].Advance >= paren.Variants[i - 1].Advance);
        }

        var arrow = Font.Horizontal(Font.GlyphIndex(0x2192));
        Assert.NotNull(arrow);
        Assert.NotEmpty(arrow.Assembly);
    }

    [Fact]
    public void MathGlyphInfoIsRead()
    {
        Assert.True(Font.Italic(Font.GlyphIndex(0x222B)) > 0);
        Assert.NotNull(Font.TopAccent(Font.GlyphIndex(0x1D44E)));
    }

    [Fact]
    public void UnknownCodePointMapsToNotdef()
    {
        Assert.Equal(0, Font.GlyphIndex(0x10FFFD));
        Assert.False(Font.HasGlyph(0x10FFFD));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(400)]
    public void InvalidFontDataIsRejected(int length)
    {
        Assert.Throws<InvalidDataException>(() => MathFont.Load(new byte[length]));
    }

    [Fact]
    public void TruncatedFontIsRejected()
    {
        using var stream = typeof(MathFont).Assembly.GetManifestResourceStream("MkTeX.STIXTwoMath-Regular.ttf")!;
        var data = new byte[4096];
        stream.ReadExactly(data);
        Assert.Throws<InvalidDataException>(() => MathFont.Load(data));
    }
}
