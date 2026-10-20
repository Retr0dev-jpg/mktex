using MkTeX.Fonts;
using MkTeX.Layout;
using MkTeX.Parsing;
using MkTeX.Rendering;

namespace MkTeX;

/// <summary>Entry point: parses LaTeX math and lays it out.</summary>
public static class MathEngine
{
    public const int MaxSourceLength = Parser.MaxLength;

    private const string ErrorColor = "#CC0000";

    /// <summary>Lays out <paramref name="source"/>. Never throws for invalid input: check <see cref="MathLayout.Error"/>.</summary>
    public static MathLayout Layout(string source, MathOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new MathOptions();
        var font = options.Font ?? MathFont.Default;
        var ctx = new Context(options.Display ? MathStyleKind.Display : MathStyleKind.Text, false, 1, options.Color, font);
        try
        {
            var parsed = Parser.Parse(source, options.Macros);
            var typesetter = new Typesetter(font);
            var box = typesetter.LayoutRoot(parsed.Root, ctx);
            MathLayout? tag = null;
            if (parsed.Tag is not null)
            {
                var tagText = parsed.TagParens ? $"({parsed.Tag})" : parsed.Tag;
                var tagBox = typesetter.LayoutNode(new TextNode(tagText, MathVariant.Normal), ctx with { Style = MathStyleKind.Text });
                tag = Flatten(tagBox, font, options.FontSize, null);
            }

            return Flatten(box, font, options.FontSize, tag);
        }
        catch (MathParseException ex)
        {
            return ErrorLayout(source, ex.ToError(), font, options.FontSize);
        }
    }

    /// <summary>Lays out and renders as a standalone SVG document fragment.</summary>
    public static string ToSvg(string source, MathOptions? options = null, SvgOptions? svg = null) =>
        SvgRenderer.Render(Layout(source, options), svg);

    /// <summary>Checks the syntax without laying out.</summary>
    public static MathError? Validate(string source, IReadOnlyDictionary<string, string>? macros = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            Parser.Parse(source, macros);
            return null;
        }
        catch (MathParseException ex)
        {
            return ex.ToError();
        }
    }

    private static MathLayout ErrorLayout(string source, MathError error, MathFont font, double fontSize)
    {
        var text = source.Length > 200 ? source[..200] + "\u2026" : source;
        text = text.Replace('\n', ' ').Replace('\r', ' ');
        var width = text.Length * 0.55 * fontSize;
        var height = 0.75 * fontSize;
        var depth = 0.25 * fontSize;
        return new MathLayout(font, fontSize, width, height, depth, [new TextItem(text, 0, height, fontSize, false, false, ErrorColor)], error, null);
    }

    private static MathLayout Flatten(Box box, MathFont font, double fontSize, MathLayout? tag)
    {
        var items = new List<MathItem>();
        double minX = 0, maxX = box.Width;
        Walk(box, 0, 0, items, fontSize, ref minX, ref maxX);
        var offset = -minX;
        if (offset > 0)
        {
            for (var i = 0; i < items.Count; i++)
            {
                items[i] = Translate(items[i], offset * fontSize);
            }
        }

        var height = box.Height * fontSize;
        var shifted = new List<MathItem>(items.Count);
        foreach (var item in items)
        {
            shifted.Add(TranslateY(item, height));
        }

        return new MathLayout(font, fontSize, (maxX - minX) * fontSize, height, box.Depth * fontSize, shifted, null, tag);
    }

    private static void Walk(Box box, double x, double y, List<MathItem> items, double px, ref double minX, ref double maxX)
    {
        switch (box)
        {
            case GlyphBox g:
                items.Add(new GlyphItem(g.Glyph, x * px, y * px, g.Scale * px, g.Color));
                break;
            case RuleBox r when r.Width > 0 && r.Height + r.Depth > 0:
                items.Add(new RuleItem(x * px, (y - r.Height) * px, r.Width * px, (r.Height + r.Depth) * px, r.Color));
                break;
            case LineBox l:
                items.Add(new LineItem((x + l.X1) * px, (y + l.Y1) * px, (x + l.X2) * px, (y + l.Y2) * px, l.Thickness * px, l.Color));
                break;
            case FrameBox f:
                items.Add(new FrameItem(x * px, (y - f.Height) * px, f.Width * px, (f.Height + f.Depth) * px, f.Thickness * px, f.Stroked, f.Stroke, f.Fill));
                break;
            case TextBox t:
                items.Add(new TextItem(t.Text, x * px, y * px, t.Scale * px, t.Bold, t.Italic, t.Color));
                break;
            case ContainerBox c:
                foreach (var child in c.Children)
                {
                    Walk(child.Box, x + child.X, y + child.Y, items, px, ref minX, ref maxX);
                }

                return;
            default:
                return;
        }

        minX = Math.Min(minX, x);
        maxX = Math.Max(maxX, x + box.Width);
    }

    private static MathItem Translate(MathItem item, double dx) => item switch
    {
        GlyphItem g => g with { X = g.X + dx },
        RuleItem r => r with { X = r.X + dx },
        LineItem l => l with { X1 = l.X1 + dx, X2 = l.X2 + dx },
        FrameItem f => f with { X = f.X + dx },
        TextItem t => t with { X = t.X + dx },
        _ => item
    };

    private static MathItem TranslateY(MathItem item, double dy) => item switch
    {
        GlyphItem g => g with { Y = g.Y + dy },
        RuleItem r => r with { Y = r.Y + dy },
        LineItem l => l with { Y1 = l.Y1 + dy, Y2 = l.Y2 + dy },
        FrameItem f => f with { Y = f.Y + dy },
        TextItem t => t with { Y = t.Y + dy },
        _ => item
    };
}
