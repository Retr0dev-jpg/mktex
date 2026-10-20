namespace MkTeX.Parsing;

public enum MathVariant
{
    Default,
    Normal,
    Bold,
    Italic,
    BoldItalic,
    Script,
    BoldScript,
    Fraktur,
    BoldFraktur,
    DoubleStruck,
    SansSerif,
    SansSerifBold,
    SansSerifItalic,
    SansSerifBoldItalic,
    Monospace
}

/// <summary>Maps letters and digits to the Unicode Mathematical Alphanumeric Symbols block.</summary>
internal static class Alphabets
{
    private static readonly Dictionary<MathVariant, int> LatinBase = new()
    {
        [MathVariant.Bold] = 0x1D400, [MathVariant.Italic] = 0x1D434, [MathVariant.BoldItalic] = 0x1D468,
        [MathVariant.Script] = 0x1D49C, [MathVariant.BoldScript] = 0x1D4D0, [MathVariant.Fraktur] = 0x1D504,
        [MathVariant.DoubleStruck] = 0x1D538, [MathVariant.BoldFraktur] = 0x1D56C, [MathVariant.SansSerif] = 0x1D5A0,
        [MathVariant.SansSerifBold] = 0x1D5D4, [MathVariant.SansSerifItalic] = 0x1D608,
        [MathVariant.SansSerifBoldItalic] = 0x1D63C, [MathVariant.Monospace] = 0x1D670
    };

    private static readonly Dictionary<MathVariant, int> DigitBase = new()
    {
        [MathVariant.Bold] = 0x1D7CE, [MathVariant.BoldItalic] = 0x1D7CE, [MathVariant.DoubleStruck] = 0x1D7D8,
        [MathVariant.SansSerif] = 0x1D7E2, [MathVariant.SansSerifItalic] = 0x1D7E2, [MathVariant.SansSerifBold] = 0x1D7EC,
        [MathVariant.SansSerifBoldItalic] = 0x1D7EC, [MathVariant.Monospace] = 0x1D7F6
    };

    private static readonly Dictionary<MathVariant, int> GreekBase = new()
    {
        [MathVariant.Bold] = 0x1D6A8, [MathVariant.Italic] = 0x1D6E2, [MathVariant.BoldItalic] = 0x1D71C,
        [MathVariant.SansSerifBold] = 0x1D756, [MathVariant.SansSerifBoldItalic] = 0x1D790
    };

    private static readonly Dictionary<(MathVariant, char), int> Holes = new()
    {
        [(MathVariant.Italic, 'h')] = 0x210E,
        [(MathVariant.Script, 'B')] = 0x212C, [(MathVariant.Script, 'E')] = 0x2130, [(MathVariant.Script, 'F')] = 0x2131,
        [(MathVariant.Script, 'H')] = 0x210B, [(MathVariant.Script, 'I')] = 0x2110, [(MathVariant.Script, 'L')] = 0x2112,
        [(MathVariant.Script, 'M')] = 0x2133, [(MathVariant.Script, 'R')] = 0x211B, [(MathVariant.Script, 'e')] = 0x212F,
        [(MathVariant.Script, 'g')] = 0x210A, [(MathVariant.Script, 'o')] = 0x2134,
        [(MathVariant.Fraktur, 'C')] = 0x212D, [(MathVariant.Fraktur, 'H')] = 0x210C, [(MathVariant.Fraktur, 'I')] = 0x2111,
        [(MathVariant.Fraktur, 'R')] = 0x211C, [(MathVariant.Fraktur, 'Z')] = 0x2128,
        [(MathVariant.DoubleStruck, 'C')] = 0x2102, [(MathVariant.DoubleStruck, 'H')] = 0x210D,
        [(MathVariant.DoubleStruck, 'N')] = 0x2115, [(MathVariant.DoubleStruck, 'P')] = 0x2119,
        [(MathVariant.DoubleStruck, 'Q')] = 0x211A, [(MathVariant.DoubleStruck, 'R')] = 0x211D,
        [(MathVariant.DoubleStruck, 'Z')] = 0x2124
    };

    // Greek variant letters follow the 25 lowercase letters in each Greek block: ∂ ϵ ϑ ϰ ϕ ϱ ϖ.
    private static readonly int[] GreekSymbols = [0x2202, 0x3F5, 0x3D1, 0x3F0, 0x3D5, 0x3F1, 0x3D6];

    /// <summary>Returns the code point that represents <paramref name="codePoint"/> in <paramref name="variant"/>.</summary>
    public static int Map(int codePoint, MathVariant variant)
    {
        var defaultStyle = variant == MathVariant.Default;
        if (defaultStyle)
        {
            if (IsLatin(codePoint))
            {
                variant = MathVariant.Italic;
            }
            else if (IsLowerGreek(codePoint) || Array.IndexOf(GreekSymbols, codePoint) > 0)
            {
                variant = MathVariant.Italic;
            }
            else if (codePoint is 0x131 or 0x237)
            {
                return codePoint == 0x131 ? 0x1D6A4 : 0x1D6A5;
            }
            else
            {
                return codePoint;
            }
        }

        if (variant == MathVariant.Normal)
        {
            return codePoint;
        }

        if (IsLatin(codePoint))
        {
            if (Holes.TryGetValue((variant, (char)codePoint), out var hole))
            {
                return hole;
            }

            if (!LatinBase.TryGetValue(variant, out var start))
            {
                return codePoint;
            }

            return start + (codePoint >= 'a' ? 26 + codePoint - 'a' : codePoint - 'A');
        }

        if (codePoint is >= '0' and <= '9')
        {
            return DigitBase.TryGetValue(variant, out var digits) ? digits + codePoint - '0' : codePoint;
        }

        var greekVariant = variant switch
        {
            MathVariant.SansSerif => MathVariant.SansSerifBold,
            MathVariant.SansSerifItalic => MathVariant.SansSerifBoldItalic,
            _ => variant
        };
        if (!GreekBase.TryGetValue(greekVariant, out var greek))
        {
            return codePoint;
        }

        if (codePoint is >= 0x391 and <= 0x3A9 && codePoint != 0x3A2)
        {
            return greek + codePoint - 0x391;
        }

        if (codePoint == 0x3F4)
        {
            return greek + 17;
        }

        if (codePoint == 0x2207)
        {
            return greek + 25;
        }

        if (IsLowerGreek(codePoint))
        {
            return greek + 26 + codePoint - 0x3B1;
        }

        var symbol = Array.IndexOf(GreekSymbols, codePoint);
        return symbol >= 0 ? greek + 51 + symbol : codePoint;
    }

    /// <summary>The variant that \boldsymbol applies to a character.</summary>
    public static MathVariant BoldSymbol(int codePoint) =>
        codePoint is >= 0x391 and <= 0x3A9 || codePoint is >= '0' and <= '9' ? MathVariant.Bold : MathVariant.BoldItalic;

    private static bool IsLatin(int c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsLowerGreek(int c) => c is >= 0x3B1 and <= 0x3C9;
}
