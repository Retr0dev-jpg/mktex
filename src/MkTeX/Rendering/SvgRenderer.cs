using System.Globalization;
using System.Net;
using System.Text;

namespace MkTeX.Rendering;

public sealed record SvgOptions
{
    /// <summary>Prefix for glyph ids in &lt;defs&gt;; make it unique per document when inlining several SVGs.</summary>
    public string IdPrefix { get; init; } = "mktex-";

    /// <summary>Accessible label (usually the TeX source).</summary>
    public string? Title { get; init; }

    /// <summary>Adds vertical-align so the SVG sits on the surrounding text baseline.</summary>
    public bool InlineBaseline { get; init; } = true;

    /// <summary>Includes the xmlns attribute (needed for standalone .svg files).</summary>
    public bool Standalone { get; init; } = true;
}

/// <summary>Renders a <see cref="MathLayout"/> as SVG markup with glyph outlines as paths.</summary>
public static class SvgRenderer
{
    public static string Render(MathLayout layout, SvgOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        options ??= new SvgOptions();
        var prefix = SanitizeId(options.IdPrefix);
        var width = Math.Max(layout.Width, 1);
        var height = Math.Max(layout.TotalHeight, 1);
        var svg = new StringBuilder();
        svg.Append("<svg");
        if (options.Standalone)
        {
            svg.Append(" xmlns=\"http://www.w3.org/2000/svg\"");
        }

        svg.Append(" width=\"").Append(N(width)).Append("\" height=\"").Append(N(height))
            .Append("\" viewBox=\"0 0 ").Append(N(width)).Append(' ').Append(N(height)).Append('"');
        if (options.InlineBaseline)
        {
            svg.Append(" style=\"vertical-align:").Append(N(-layout.Depth)).Append("px\"");
        }

        svg.Append(" role=\"img\"");
        if (options.Title is not null)
        {
            svg.Append(" aria-label=\"").Append(WebUtility.HtmlEncode(options.Title)).Append('"');
        }

        svg.Append('>');
        if (options.Title is not null)
        {
            svg.Append("<title>").Append(WebUtility.HtmlEncode(options.Title)).Append("</title>");
        }

        var glyphs = layout.Items.OfType<GlyphItem>().Select(g => g.Glyph).Distinct().Order().ToList();
        if (glyphs.Count > 0)
        {
            svg.Append("<defs>");
            foreach (var glyph in glyphs)
            {
                svg.Append("<path id=\"").Append(prefix).Append('g').Append(glyph.ToString(CultureInfo.InvariantCulture))
                    .Append("\" d=\"").Append(layout.Font.Outline(glyph).ToSvgPathData()).Append("\"/>");
            }

            svg.Append("</defs>");
        }

        foreach (var item in layout.Items)
        {
            switch (item)
            {
                case GlyphItem g:
                    svg.Append("<use href=\"#").Append(prefix).Append('g').Append(g.Glyph.ToString(CultureInfo.InvariantCulture))
                        .Append("\" transform=\"translate(").Append(N(g.X)).Append(' ').Append(N(g.Y)).Append(") scale(").Append(N(g.Size))
                        .Append(")\" fill=\"").Append(Color(g.Color)).Append("\"/>");
                    break;
                case RuleItem r:
                    svg.Append("<rect x=\"").Append(N(r.X)).Append("\" y=\"").Append(N(r.Y)).Append("\" width=\"").Append(N(r.Width))
                        .Append("\" height=\"").Append(N(r.Height)).Append("\" fill=\"").Append(Color(r.Color)).Append("\"/>");
                    break;
                case LineItem l:
                    svg.Append("<line x1=\"").Append(N(l.X1)).Append("\" y1=\"").Append(N(l.Y1)).Append("\" x2=\"").Append(N(l.X2))
                        .Append("\" y2=\"").Append(N(l.Y2)).Append("\" stroke=\"").Append(Color(l.Color)).Append("\" stroke-width=\"")
                        .Append(N(l.Thickness)).Append("\" stroke-linecap=\"round\"/>");
                    break;
                case FrameItem f:
                    var inset = f.Stroked ? f.Thickness / 2 : 0;
                    svg.Append("<rect x=\"").Append(N(f.X + inset)).Append("\" y=\"").Append(N(f.Y + inset)).Append("\" width=\"")
                        .Append(N(f.Width - 2 * inset)).Append("\" height=\"").Append(N(f.Height - 2 * inset)).Append("\" fill=\"")
                        .Append(f.Fill is null ? "none" : Color(f.Fill)).Append('"');
                    if (f.Stroked)
                    {
                        svg.Append(" stroke=\"").Append(Color(f.Stroke)).Append("\" stroke-width=\"").Append(N(f.Thickness)).Append('"');
                    }

                    svg.Append("/>");
                    break;
                case TextItem t:
                    svg.Append("<text x=\"").Append(N(t.X)).Append("\" y=\"").Append(N(t.Y)).Append("\" font-size=\"").Append(N(t.Size))
                        .Append("\" font-family=\"'STIX Two Math','Cambria Math','Segoe UI',serif\" fill=\"").Append(Color(t.Color)).Append('"');
                    if (t.Bold)
                    {
                        svg.Append(" font-weight=\"bold\"");
                    }

                    if (t.Italic)
                    {
                        svg.Append(" font-style=\"italic\"");
                    }

                    svg.Append(" xml:space=\"preserve\">").Append(WebUtility.HtmlEncode(t.Text)).Append("</text>");
                    break;
            }
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    private static string Color(string? color) => color ?? "currentColor";

    private static string N(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

    private static string SanitizeId(string prefix)
    {
        var builder = new StringBuilder(prefix.Length);
        foreach (var c in prefix)
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-');
        }

        return builder.ToString();
    }
}
