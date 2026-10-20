using MkTeX.Fonts;

namespace MkTeX;

/// <summary>Options for <see cref="MathEngine"/>.</summary>
public sealed record MathOptions
{
    /// <summary>Font size in pixels (1 em).</summary>
    public double FontSize { get; init; } = 16;

    /// <summary>Display style (block formula) instead of text style (inline).</summary>
    public bool Display { get; init; }

    /// <summary>Default color as #RRGGBB; null means the renderer's foreground color.</summary>
    public string? Color { get; init; }

    /// <summary>Extra macros, name (with or without backslash) to body using #1..#9.</summary>
    public IReadOnlyDictionary<string, string>? Macros { get; init; }

    /// <summary>Math font; defaults to the bundled STIX Two Math.</summary>
    public MathFont? Font { get; init; }
}

/// <summary>Drawing primitive in pixels. Coordinates grow right and down from the top-left corner of the layout.</summary>
public abstract record MathItem;

/// <summary>A font glyph with its origin (baseline, left side bearing origin) at X, Y, scaled to Size pixels per em.</summary>
public sealed record GlyphItem(ushort Glyph, double X, double Y, double Size, string? Color) : MathItem;

/// <summary>A filled rectangle.</summary>
public sealed record RuleItem(double X, double Y, double Width, double Height, string? Color) : MathItem;

/// <summary>A straight stroke.</summary>
public sealed record LineItem(double X1, double Y1, double X2, double Y2, double Thickness, string? Color) : MathItem;

/// <summary>A rectangle with optional fill and stroke (\boxed, \colorbox, \fcolorbox).</summary>
public sealed record FrameItem(double X, double Y, double Width, double Height, double Thickness, bool Stroked, string? Stroke, string? Fill) : MathItem;

/// <summary>Text the math font cannot draw; renderers use a system font. Y is the baseline.</summary>
public sealed record TextItem(string Text, double X, double Y, double Size, bool Bold, bool Italic, string? Color) : MathItem;

/// <summary>The typeset formula: a list of drawing items and its metrics in pixels.</summary>
public sealed class MathLayout
{
    internal MathLayout(MathFont font, double fontSize, double width, double height, double depth, IReadOnlyList<MathItem> items, MathError? error, MathLayout? tag)
    {
        Font = font;
        FontSize = fontSize;
        Width = width;
        Height = height;
        Depth = depth;
        Items = items;
        Error = error;
        Tag = tag;
    }

    public MathFont Font { get; }

    public double FontSize { get; }

    public double Width { get; }

    /// <summary>Distance from the top to the baseline.</summary>
    public double Height { get; }

    /// <summary>Distance from the baseline to the bottom.</summary>
    public double Depth { get; }

    public double TotalHeight => Height + Depth;

    public IReadOnlyList<MathItem> Items { get; }

    /// <summary>Set when the source could not be parsed; Items then show the source text.</summary>
    public MathError? Error { get; }

    /// <summary>Equation tag from \tag, laid out separately so hosts can align it to the right margin.</summary>
    public MathLayout? Tag { get; }
}
