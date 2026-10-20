using System.Globalization;
using System.Text;

namespace MkTeX.Fonts;

public enum PathVerb
{
    MoveTo,
    LineTo,
    QuadTo,
    Close
}

/// <summary>One outline command in em units, y pointing up. <see cref="X1"/>/<see cref="Y1"/> is the control point of a quadratic.</summary>
public readonly record struct PathCommand(PathVerb Verb, double X, double Y, double X1 = 0, double Y1 = 0);

public readonly record struct GlyphBounds(double XMin, double YMin, double XMax, double YMax)
{
    public bool IsEmpty => XMax <= XMin || YMax <= YMin;
}

public sealed class GlyphOutline(IReadOnlyList<PathCommand> commands)
{
    public static readonly GlyphOutline Empty = new([]);

    public IReadOnlyList<PathCommand> Commands { get; } = commands;

    /// <summary>SVG path data in em units with y flipped to point down, so it can be scaled by the font size.</summary>
    public string ToSvgPathData()
    {
        var builder = new StringBuilder(Commands.Count * 16);
        foreach (var c in Commands)
        {
            switch (c.Verb)
            {
                case PathVerb.MoveTo:
                    builder.Append('M').Append(N(c.X)).Append(' ').Append(N(-c.Y));
                    break;
                case PathVerb.LineTo:
                    builder.Append('L').Append(N(c.X)).Append(' ').Append(N(-c.Y));
                    break;
                case PathVerb.QuadTo:
                    builder.Append('Q').Append(N(c.X1)).Append(' ').Append(N(-c.Y1)).Append(' ')
                        .Append(N(c.X)).Append(' ').Append(N(-c.Y));
                    break;
                default:
                    builder.Append('Z');
                    break;
            }
        }

        return builder.ToString();
    }

    private static string N(double value) => Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
}
