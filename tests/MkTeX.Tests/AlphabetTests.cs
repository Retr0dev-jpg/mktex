using MkTeX.Parsing;

namespace MkTeX.Tests;

public class AlphabetTests
{
    [Theory]
    [InlineData('x', MathVariant.Default, 0x1D465)]
    [InlineData('h', MathVariant.Default, 0x210E)]
    [InlineData('A', MathVariant.Bold, 0x1D400)]
    [InlineData('z', MathVariant.Bold, 0x1D433)]
    [InlineData('R', MathVariant.DoubleStruck, 0x211D)]
    [InlineData('A', MathVariant.DoubleStruck, 0x1D538)]
    [InlineData('L', MathVariant.Script, 0x2112)]
    [InlineData('A', MathVariant.Script, 0x1D49C)]
    [InlineData('H', MathVariant.Fraktur, 0x210C)]
    [InlineData('g', MathVariant.Fraktur, 0x1D524)]
    [InlineData('a', MathVariant.Monospace, 0x1D68A)]
    [InlineData('7', MathVariant.Bold, 0x1D7D5)]
    [InlineData('1', MathVariant.DoubleStruck, 0x1D7D9)]
    [InlineData('5', MathVariant.Default, '5')]
    [InlineData('x', MathVariant.Normal, 'x')]
    [InlineData(0x3B1, MathVariant.Default, 0x1D6FC)]
    [InlineData(0x393, MathVariant.Default, 0x393)]
    [InlineData(0x393, MathVariant.Bold, 0x1D6AA)]
    [InlineData(0x3A3, MathVariant.Italic, 0x1D6F4)]
    [InlineData(0x3D5, MathVariant.Default, 0x1D719)]
    [InlineData(0x2207, MathVariant.Bold, 0x1D6C1)]
    [InlineData(0x3B1, MathVariant.SansSerifBold, 0x1D770)]
    public void MapsToMathematicalAlphanumerics(int codePoint, MathVariant variant, int expected)
    {
        Assert.Equal(expected, Alphabets.Map(codePoint, variant));
    }

    [Fact]
    public void BoldSymbolKeepsCapitalGreekUpright()
    {
        Assert.Equal(MathVariant.Bold, Alphabets.BoldSymbol(0x393));
        Assert.Equal(MathVariant.BoldItalic, Alphabets.BoldSymbol('x'));
    }

    [Fact]
    public void EveryMappedLetterExistsInTheFont()
    {
        var font = Fonts.MathFont.Default;
        var variants = Enum.GetValues<MathVariant>().Where(v => v is not MathVariant.Default and not MathVariant.Normal);
        foreach (var variant in variants)
        {
            for (var c = 'A'; c <= 'z'; c++)
            {
                if (!char.IsAsciiLetter(c))
                {
                    continue;
                }

                var mapped = Alphabets.Map(c, variant);
                Assert.True(font.HasGlyph(mapped), $"{variant} {c} -> U+{mapped:X}");
            }
        }
    }
}
