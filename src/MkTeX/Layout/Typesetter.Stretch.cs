using MkTeX.Fonts;
using MkTeX.Parsing;

namespace MkTeX.Layout;

internal sealed partial class Typesetter
{
    private const int MaxExtenderRepeats = 200;

    /// <summary>A vertical delimiter at least <paramref name="target"/> tall (height + depth), centered on the math axis.</summary>
    private Box Delimiter(int codePoint, double target, Context ctx)
    {
        if (codePoint == 0)
        {
            return Kern(NullDelimiterSpace * ctx.Scale);
        }

        var box = VerticalStretch(codePoint, target, ctx);
        return CenterOnAxis(box, ctx);
    }

    private Box VerticalStretch(int codePoint, double target, Context ctx)
    {
        var glyph = _font.GlyphIndex(codePoint);
        if (glyph == 0)
        {
            return FallbackText(char.ConvertFromUtf32(codePoint), ctx, MathVariant.Normal);
        }

        var scale = ctx.Scale;
        var baseBox = GlyphById(glyph, ctx);
        if (baseBox.Height + baseBox.Depth >= target)
        {
            return baseBox;
        }

        var construction = _font.Vertical(glyph);
        if (construction is null)
        {
            return baseBox;
        }

        foreach (var variant in construction.Variants)
        {
            if (variant.Advance * scale >= target)
            {
                return GlyphById(variant.Glyph, ctx);
            }
        }

        if (construction.Assembly.Count > 0)
        {
            return Assemble(construction.Assembly, target, vertical: true, ctx);
        }

        return construction.Variants.Count > 0 ? GlyphById(construction.Variants[^1].Glyph, ctx) : baseBox;
    }

    /// <summary>
    /// A horizontally stretched glyph whose ink spans [0, Width]. With <paramref name="keepVertical"/> the font's
    /// vertical placement is preserved (accents); otherwise the ink bottom sits on the baseline.
    /// </summary>
    private Box HorizontalStretch(int codePoint, double target, Context ctx, bool keepVertical)
    {
        var glyph = _font.GlyphIndex(codePoint);
        if (glyph == 0)
        {
            return FallbackText(char.ConvertFromUtf32(codePoint), ctx, MathVariant.Normal);
        }

        var scale = ctx.Scale;
        Box result;
        var construction = _font.Horizontal(glyph);
        var chosen = glyph;
        var found = InkWidth(glyph) * scale >= target;
        if (!found && construction is not null)
        {
            foreach (var variant in construction.Variants)
            {
                chosen = variant.Glyph;
                if (InkWidth(variant.Glyph) * scale >= target)
                {
                    found = true;
                    break;
                }
            }
        }

        if (!found && construction is { Assembly.Count: > 0 })
        {
            result = Assemble(construction.Assembly, target, vertical: false, ctx);
        }
        else
        {
            var bounds = _font.Bounds(chosen);
            var inked = new ContainerBox();
            inked.Add(GlyphById(chosen, ctx), -bounds.XMin * scale, 0);
            result = inked.Fit((bounds.XMax - bounds.XMin) * scale);
        }

        return keepVertical ? result : Shift(result, -result.Depth);
    }

    private double InkWidth(ushort glyph)
    {
        var bounds = _font.Bounds(glyph);
        return bounds.XMax - bounds.XMin;
    }

    private Box Assemble(IReadOnlyList<GlyphPart> parts, double target, bool vertical, Context ctx)
    {
        var scale = ctx.Scale;
        var minOverlap = _font.MinConnectorOverlap * scale;
        var hasExtender = parts.Any(p => p.IsExtender);
        List<GlyphPart> sequence = [];
        for (var repeats = hasExtender ? 1 : 0; repeats <= MaxExtenderRepeats; repeats++)
        {
            sequence = [];
            foreach (var part in parts)
            {
                if (part.IsExtender)
                {
                    for (var r = 0; r < repeats; r++)
                    {
                        sequence.Add(part);
                    }
                }
                else
                {
                    sequence.Add(part);
                }
            }

            var maxLength = sequence.Sum(p => p.FullAdvance) * scale - minOverlap * (sequence.Count - 1);
            if (maxLength >= target || !hasExtender)
            {
                break;
            }
        }

        var junctions = Math.Max(1, sequence.Count - 1);
        var longest = sequence.Sum(p => p.FullAdvance) * scale - minOverlap * (sequence.Count - 1);
        var shrinkPerJunction = Math.Max(0, longest - target) / junctions;

        var box = new ContainerBox();
        var cursor = 0.0;
        for (var i = 0; i < sequence.Count; i++)
        {
            var part = sequence[i];
            var glyphBox = GlyphById(part.Glyph, ctx);
            var bounds = _font.Bounds(part.Glyph);
            if (vertical)
            {
                box.Add(glyphBox, 0, -(cursor - bounds.YMin * scale));
            }
            else
            {
                box.Add(glyphBox, cursor - bounds.XMin * scale, 0);
            }

            if (i + 1 < sequence.Count)
            {
                var next = sequence[i + 1];
                var capacity = Math.Max(minOverlap, Math.Min(part.EndConnector, next.StartConnector) * scale);
                var overlap = Math.Min(capacity, minOverlap + shrinkPerJunction);
                cursor += part.FullAdvance * scale - overlap;
            }
            else
            {
                cursor += part.FullAdvance * scale;
            }
        }

        if (vertical)
        {
            var width = sequence.Max(p => _font.Advance(p.Glyph)) * scale;
            box.Fit(width);
            box.Height = cursor;
            box.Depth = 0;
            return box;
        }

        return box.Fit(cursor);
    }

    private Box LayoutSizedDelimiter(DelimNode delim, Context ctx) => Delimiter(delim.CodePoint, delim.Size * ctx.Scale, ctx);

    private Box LayoutLeftRight(LeftRightNode node, Context ctx)
    {
        var parts = node.Parts.Select(p => LayoutNode(p, ctx)).ToList();
        var height = parts.Max(p => p.Height);
        var depth = parts.Max(p => p.Depth);
        var axis = ctx.Axis;
        var delta = Math.Max(height - axis, depth + axis);
        // TeX: \delimiterfactor = 901, \delimitershortfall = 5pt.
        var target = Math.Max(delta * 2 * 0.901, 2 * delta - 0.5 * ctx.Scale);

        var row = new Row();
        row.Add(Delimiter(node.Left, target, ctx));
        for (var i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                row.Add(Delimiter(node.Middles[i - 1], target, ctx));
            }

            row.Add(parts[i]);
        }

        row.Add(Delimiter(node.Right, target, ctx));
        return row.Build();
    }
}
