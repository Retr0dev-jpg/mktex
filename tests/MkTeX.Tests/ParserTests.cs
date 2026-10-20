using MkTeX.Parsing;

namespace MkTeX.Tests;

public class ParserTests
{
    private static ListNode Parse(string source) => Parser.Parse(source).Root;

    private static MathErrorCode Error(string source) =>
        Assert.Throws<MathParseException>(() => Parser.Parse(source)).Code;

    [Fact]
    public void ParsesScriptsOnTheRightNucleus()
    {
        var root = Parse("x_i^2");
        var scripts = Assert.IsType<ScriptsNode>(Assert.Single(root.Items));
        Assert.Equal(0x78, Assert.IsType<CharNode>(scripts.Nucleus).CodePoint);
        Assert.NotNull(scripts.Sup);
        Assert.NotNull(scripts.Sub);
    }

    [Fact]
    public void PrimesBecomePrimeNodes()
    {
        var scripts = Assert.IsType<ScriptsNode>(Assert.Single(Parse("f''^2").Items));
        var primes = Assert.IsType<PrimesNode>(scripts.Sup);
        Assert.Equal(2, primes.Count);
        Assert.NotNull(primes.Extra);
        Assert.IsType<PrimesNode>(Assert.IsType<ScriptsNode>(Assert.Single(Parse("x^{\\prime}").Items)).Sup);
    }

    [Fact]
    public void MinusAndAsteriskAreBinaryOperators()
    {
        var items = Parse("a-b*c").Items.Cast<CharNode>().ToList();
        Assert.Equal(0x2212, items[1].CodePoint);
        Assert.Equal(AtomType.Bin, items[1].Type);
        Assert.Equal(0x2217, items[3].CodePoint);
    }

    [Fact]
    public void InfixFractionsSplitTheGroup()
    {
        var frac = Assert.IsType<FracNode>(Assert.Single(Parse("a+b \\over c").Items));
        Assert.Equal(3, Assert.IsType<ListNode>(frac.Numerator).Items.Count);
        var choose = Assert.IsType<FracNode>(Assert.Single(Parse("n \\choose k").Items));
        Assert.Equal(0d, choose.Thickness);
        Assert.Equal('(', choose.Left);
    }

    [Fact]
    public void BinomUsesGenfracMacro()
    {
        var frac = Assert.IsType<FracNode>(Assert.Single(Parse("\\binom{n}{k}").Items));
        Assert.Equal(0d, frac.Thickness);
        Assert.Equal(')', frac.Right);
        Assert.Equal(FracStyle.Display, Assert.IsType<FracNode>(Assert.Single(Parse("\\dfrac12").Items)).Style);
    }

    [Fact]
    public void LeftRightCollectsMiddles()
    {
        var lr = Assert.IsType<LeftRightNode>(Assert.Single(Parse("\\left\\{ x \\middle| x>0 \\right.").Items));
        Assert.Equal('{', lr.Left);
        Assert.Equal(0, lr.Right);
        Assert.Equal(2, lr.Parts.Count);
        Assert.Equal('|', Assert.Single(lr.Middles));
    }

    [Fact]
    public void EnvironmentsBuildArrays()
    {
        var lr = Assert.IsType<LeftRightNode>(Assert.Single(Parse("\\begin{pmatrix} a & b \\\\ c & d \\\\ \\end{pmatrix}").Items));
        var array = Assert.IsType<ArrayNode>(Assert.Single(lr.Parts));
        Assert.Equal(2, array.Rows.Count);
        Assert.Equal(2, array.Columns.Count);

        var table = Assert.IsType<ArrayNode>(Assert.Single(Parse("\\begin{array}{|l|c|} \\hline a & b \\\\ \\hline \\end{array}").Items));
        Assert.Equal([ColumnAlign.Left, ColumnAlign.Center], table.Columns);
        Assert.Equal([true, true, true], table.ColumnLines);
        Assert.True(table.RowLines[0]);
        Assert.True(table.RowLines[1]);

        var aligned = Assert.IsType<ArrayNode>(Assert.Single(Parse("\\begin{aligned} a &= b \\tag{1} \\\\ c &= d \\end{aligned}").Items));
        Assert.True(aligned.AlignPairs);
        Assert.Equal("(1)", aligned.Tags[0]);
        Assert.Null(aligned.Tags[1]);
    }

    [Fact]
    public void MacrosExpandWithArgumentsAndDefaults()
    {
        var root = Parse("\\newcommand{\\pair}[2][x]{(#1,#2)} \\pair{y} \\pair[z]{w}");
        var codes = root.Items.OfType<CharNode>().Select(c => c.CodePoint).ToList();
        Assert.Equal(['(', 'x', ',', 'y', ')', '(', 'z', ',', 'w', ')'], codes);

        var custom = Parser.Parse("\\RR", new Dictionary<string, string> { ["RR"] = "\\mathbb{R}" }).Root;
        Assert.Equal(MathVariant.DoubleStruck, Assert.IsType<CharNode>(Assert.Single(Assert.IsType<ListNode>(Assert.Single(custom.Items)).Items)).Variant);
    }

    [Fact]
    public void DeclareMathOperatorDefinesAnOperator()
    {
        var root = Parse("\\DeclareMathOperator{\\Tr}{Tr} \\Tr A");
        Assert.IsType<OpNode>(root.Items[0]);
    }

    [Fact]
    public void FontCommandsSetVariants()
    {
        Assert.Equal(MathVariant.Bold, Assert.IsType<CharNode>(Assert.Single(Assert.IsType<ListNode>(Assert.Single(Parse("\\mathbf{x}").Items)).Items)).Variant);
        var bold = Assert.IsType<ListNode>(Assert.Single(Parse("\\boldsymbol{\\Gamma x}").Items)).Items.Cast<CharNode>().ToList();
        Assert.Equal(MathVariant.Bold, bold[0].Variant);
        Assert.Equal(MathVariant.BoldItalic, bold[1].Variant);
    }

    [Fact]
    public void TextModeKeepsSpacesAndEntersMath()
    {
        var text = Assert.IsType<ListNode>(Assert.Single(Parse("\\text{if $x$ is  big}").Items));
        Assert.Equal("if ", Assert.IsType<TextNode>(text.Items[0]).Text);
        Assert.IsType<ListNode>(text.Items[1]);
        Assert.Equal(" is big", Assert.IsType<TextNode>(text.Items[2]).Text);
        Assert.Equal("é", Assert.IsType<TextNode>(Assert.Single(Assert.IsType<ListNode>(Assert.Single(Parse("\\text{\\'e}").Items)).Items)).Text);
    }

    [Fact]
    public void ColorsAreNormalized()
    {
        Assert.Equal("#FF0000", Assert.IsType<ColorNode>(Assert.Single(Parse("\\textcolor{red}{x}").Items)).Color);
        Assert.Equal("#00FF80", Assert.IsType<ColorNode>(Assert.Single(Parse("\\textcolor[HTML]{00ff80}{x}").Items)).Color);
        Assert.Equal("#FF0000", Assert.IsType<ColorNode>(Assert.Single(Parse("\\textcolor[rgb]{1,0,0}{x}").Items)).Color);
        Assert.Equal(MathErrorCode.InvalidArgument, Error("\\textcolor{notacolor}{x}"));
    }

    [Fact]
    public void NotUsesNegatedCodePointsWhenAvailable()
    {
        Assert.Equal(0x2260, Assert.IsType<CharNode>(Assert.Single(Parse("\\not=").Items)).CodePoint);
        var overlay = Assert.IsType<ClassNode>(Assert.Single(Parse("\\not\\triangleleft").Items));
        Assert.IsType<OverlayNode>(overlay.Body);
    }

    [Theory]
    [InlineData("\\foo", MathErrorCode.UnknownCommand)]
    [InlineData("\\frac{1}{", MathErrorCode.UnbalancedGroup)]
    [InlineData("x}", MathErrorCode.UnbalancedGroup)]
    [InlineData("\\frac{1}", MathErrorCode.MissingArgument)]
    [InlineData("x^1^2", MathErrorCode.DoubleScript)]
    [InlineData("x_1_2", MathErrorCode.DoubleScript)]
    [InlineData("\\left( x", MathErrorCode.MissingDelimiter)]
    [InlineData("x \\right)", MathErrorCode.MissingDelimiter)]
    [InlineData("\\left\\alpha x \\right)", MathErrorCode.InvalidDelimiter)]
    [InlineData("\\begin{foo} x \\end{foo}", MathErrorCode.UnknownEnvironment)]
    [InlineData("\\begin{matrix} x \\end{pmatrix}", MathErrorCode.MismatchedEnvironment)]
    [InlineData("\\begin{matrix} x", MathErrorCode.MismatchedEnvironment)]
    [InlineData("a & b", MathErrorCode.UnexpectedToken)]
    [InlineData("\\hspace{3furlongs}", MathErrorCode.InvalidArgument)]
    [InlineData("\\begin{array}{cz} a \\end{array}", MathErrorCode.InvalidArgument)]
    [InlineData("\\sqrt", MathErrorCode.MissingArgument)]
    [InlineData("\\", MathErrorCode.UnexpectedEnd)]
    [InlineData("\\text{\\foo}", MathErrorCode.UnknownCommand)]
    [InlineData("\\newcommand{\\alpha}{x}", MathErrorCode.InvalidArgument)]
    public void ReportsErrorCodes(string source, MathErrorCode expected)
    {
        Assert.Equal(expected, Error(source));
    }

    [Fact]
    public void ErrorsCarryTheSourcePosition()
    {
        var error = Assert.Throws<MathParseException>(() => Parser.Parse("a + \\nope"));
        Assert.Equal(4, error.Position);
    }

    [Fact]
    public void RecursiveMacrosHitTheExpansionLimit()
    {
        Assert.Equal(MathErrorCode.MacroLimit, Error("\\def\\a{\\a}\\a"));
        Assert.Equal(MathErrorCode.MacroLimit, Error("\\def\\a{\\a\\a}\\a"));
    }

    [Fact]
    public void ExponentialMacrosHitTheTokenLimit()
    {
        var source = "\\def\\a{xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx}" +
            "\\def\\b{\\a\\a\\a\\a\\a\\a\\a\\a\\a\\a}\\def\\c{\\b\\b\\b\\b\\b\\b\\b\\b\\b\\b}\\def\\d{\\c\\c\\c\\c\\c\\c\\c\\c\\c\\c}\\d\\d\\d";
        Assert.Equal(MathErrorCode.MacroLimit, Error(source));
    }

    [Fact]
    public void LongSourcesAreRejected()
    {
        Assert.Equal(MathErrorCode.TooLong, Error(new string('x', Parser.MaxLength + 1)));
    }

    [Fact]
    public void DeepNestingIsRejected()
    {
        Assert.Equal(MathErrorCode.TooDeep, Error(new string('{', 200) + new string('}', 200)));
        Assert.Equal(MathErrorCode.TooDeep, Error(string.Concat(Enumerable.Repeat("\\sqrt{", 150)) + new string('}', 150)));
    }

    [Fact]
    public void CommentsAreIgnored()
    {
        Assert.Equal(2, Parse("a % comment \\foo\n b").Items.Count);
    }

    [Theory]
    [InlineData("H2O", "\\mathrm{H}_{2}\\mathrm{O}")]
    [InlineData("Fe^{3+}", "\\mathrm{Fe}^{3+}")]
    [InlineData("A -> B", "\\mathrm{A}\\ \\xrightarrow{}\\ \\mathrm{B}")]
    [InlineData("2H2", "2\\,\\mathrm{H}_{2}")]
    [InlineData("H+", "\\mathrm{H}^{+}")]
    public void ChemistryTranslatesToTeX(string ce, string expected)
    {
        Assert.Equal(expected, Chemistry.ToTeX(ce));
        Parser.Parse(Chemistry.ToTeX(ce));
    }

    [Theory]
    [InlineData("3pt", 0.3, false)]
    [InlineData("-1.5em", -1.5, false)]
    [InlineData("18mu", 18, true)]
    [InlineData("1,5ex", 0.6465, false)]
    public void ParsesDimensions(string text, double em, bool mu)
    {
        var (value, isMu) = Parser.ParseDimension(text, 0);
        Assert.Equal(em, value, 4);
        Assert.Equal(mu, isMu);
    }

    [Fact]
    public void EverySymbolCommandParses()
    {
        foreach (var name in Symbols.Names)
        {
            var exception = Record.Exception(() => Parser.Parse("\\" + name));
            Assert.True(exception is null, $"\\{name}: {exception?.Message}");
        }
    }
}
