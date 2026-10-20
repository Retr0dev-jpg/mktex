using System.Globalization;
using MkTeX.Fonts;

internal static class Probe
{
    public static void Run(int[] codePoints)
    {
        var font = MathFont.Default;
        foreach (var cp in codePoints)
        {
            var g = font.GlyphIndex(cp);
            Print(font, $"U+{cp:X4}", g);
            if (font.Vertical(g) is { } v)
            {
                foreach (var variant in v.Variants)
                {
                    Print(font, $"  v{variant.Advance.ToString("F3", CultureInfo.InvariantCulture)}", variant.Glyph);
                }
            }
        }
    }

    private static void Print(MathFont font, string label, ushort g)
    {
        var b = font.Bounds(g);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{label} g={g} adv={font.Advance(g):F3} x=[{b.XMin:F3},{b.XMax:F3}] y=[{b.YMin:F3},{b.YMax:F3}] italic={font.Italic(g):F3}"));
    }
}
