namespace MkTeX.Layout;

/// <summary>A TeX-style box in em units: Height above the baseline, Depth below, y grows downward.</summary>
internal abstract class Box
{
    public double Width { get; set; }

    public double Height { get; set; }

    public double Depth { get; set; }
}

internal sealed class GlyphBox : Box
{
    public GlyphBox(ushort glyph, double scale, double italic, string? color)
    {
        Glyph = glyph;
        Scale = scale;
        Italic = italic;
        Color = color;
    }

    public ushort Glyph { get; }

    public double Scale { get; }

    public double Italic { get; }

    public string? Color { get; }
}

internal sealed class RuleBox(string? color) : Box
{
    public string? Color { get; } = color;
}

internal sealed class LineBox(double x1, double y1, double x2, double y2, double thickness, string? color) : Box
{
    public double X1 { get; } = x1;

    public double Y1 { get; } = y1;

    public double X2 { get; } = x2;

    public double Y2 { get; } = y2;

    public double Thickness { get; } = thickness;

    public string? Color { get; } = color;
}

internal sealed class FrameBox(double thickness, bool stroked, string? stroke, string? fill) : Box
{
    public double Thickness { get; } = thickness;

    public bool Stroked { get; } = stroked;

    public string? Stroke { get; } = stroke;

    public string? Fill { get; } = fill;
}

/// <summary>Text for characters that the math font cannot draw.</summary>
internal sealed class TextBox(string text, double scale, bool bold, bool italic, string? color) : Box
{
    public string Text { get; } = text;

    public double Scale { get; } = scale;

    public bool Bold { get; } = bold;

    public bool Italic { get; } = italic;

    public string? Color { get; } = color;
}

internal readonly record struct Placed(Box Box, double X, double Y);

internal sealed class ContainerBox : Box
{
    public List<Placed> Children { get; } = [];

    public void Add(Box box, double x, double y) => Children.Add(new Placed(box, x, y));

    /// <summary>Sets Height and Depth from the children; Width is left to the caller.</summary>
    public ContainerBox Fit(double width)
    {
        Width = width;
        double height = 0, depth = 0;
        foreach (var child in Children)
        {
            height = Math.Max(height, child.Box.Height - child.Y);
            depth = Math.Max(depth, child.Box.Depth + child.Y);
        }

        Height = height;
        Depth = depth;
        return this;
    }
}

/// <summary>Builds a horizontal list with vertical shifts.</summary>
internal sealed class Row
{
    private readonly ContainerBox _box = new();
    private double _x;

    public double X => _x;

    public void Add(Box box, double shiftDown = 0)
    {
        _box.Add(box, _x, shiftDown);
        _x += box.Width;
    }

    public void Kern(double width) => _x += width;

    public ContainerBox Build() => _box.Fit(_x);
}
