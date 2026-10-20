using MkTeX.Parsing;

namespace MkTeX.Layout;

internal sealed partial class Typesetter
{
    private const double BaselineSkip = 1.2;
    private const double TagGap = 2;

    private Box LayoutArray(ArrayNode array, Context ctx)
    {
        var cellCtx = array.CellStyle is { } style ? ctx with { Style = style, Cramped = false } : ctx;
        var scale = ctx.Scale;
        var columnCount = array.Columns.Count;
        var cells = array.Rows.Select(row => row.Select(cell => LayoutNode(cell, cellCtx)).ToList()).ToList();
        var tagBoxes = array.Tags.Select(t => t is null ? null : LayoutText(new TextNode(t, MathVariant.Normal), ctx with { Style = MathStyleKind.Text })).ToList();

        var widths = new double[columnCount];
        foreach (var row in cells)
        {
            for (var c = 0; c < row.Count && c < columnCount; c++)
            {
                widths[c] = Math.Max(widths[c], row[c].Width);
            }
        }

        var gaps = new double[Math.Max(0, columnCount - 1)];
        for (var c = 0; c < gaps.Length; c++)
        {
            gaps[c] = array.AlignPairs
                ? (c % 2 == 0 ? 0 : array.ColumnSpacing * scale)
                : array.ColumnSpacing * scale;
        }

        var lineWidth = LineThickness * scale;
        var columnX = new double[columnCount];
        var x = array.ColumnLines.Count > 0 && array.ColumnLines[0] ? lineWidth + 0.25 * scale : 0;
        for (var c = 0; c < columnCount; c++)
        {
            columnX[c] = x;
            x += widths[c];
            if (c < gaps.Length)
            {
                x += gaps[c];
            }
        }

        if (array.ColumnLines.Count > columnCount && array.ColumnLines[columnCount])
        {
            x += 0.25 * scale + lineWidth;
        }

        var tableWidth = x;
        var minHeight = 0.7 * BaselineSkip * array.RowSpacing * scale;
        var minDepth = 0.3 * BaselineSkip * array.RowSpacing * scale;
        var heights = new double[cells.Count];
        var depths = new double[cells.Count];
        for (var r = 0; r < cells.Count; r++)
        {
            heights[r] = Math.Max(minHeight, cells[r].Count == 0 ? 0 : cells[r].Max(b => b.Height));
            depths[r] = Math.Max(minDepth, cells[r].Count == 0 ? 0 : cells[r].Max(b => b.Depth));
            if (tagBoxes[r] is { } tag)
            {
                heights[r] = Math.Max(heights[r], tag.Height);
                depths[r] = Math.Max(depths[r], tag.Depth);
            }
        }

        var hlineGap = 0.15 * scale;
        var box = new ContainerBox();
        var y = 0.0;
        var baselines = new double[cells.Count];
        for (var r = 0; r <= cells.Count; r++)
        {
            if (r < array.RowLines.Count && array.RowLines[r])
            {
                box.Add(Rule(tableWidth, -y, -(y + lineWidth), ctx.Color), 0, 0);
                y += lineWidth + (r < cells.Count ? hlineGap : 0);
            }

            if (r == cells.Count)
            {
                break;
            }

            y += heights[r];
            baselines[r] = y;
            y += depths[r];
        }

        var total = y;
        for (var r = 0; r < cells.Count; r++)
        {
            for (var c = 0; c < cells[r].Count && c < columnCount; c++)
            {
                var cell = cells[r][c];
                var offset = array.Columns[c] switch
                {
                    ColumnAlign.Left => 0,
                    ColumnAlign.Right => widths[c] - cell.Width,
                    _ => (widths[c] - cell.Width) / 2
                };
                box.Add(cell, columnX[c] + offset, baselines[r]);
            }
        }

        for (var c = 0; c <= columnCount && c < array.ColumnLines.Count; c++)
        {
            if (!array.ColumnLines[c])
            {
                continue;
            }

            double lineX = c == 0 ? 0
                : c == columnCount ? tableWidth - lineWidth
                : columnX[c] - gaps[c - 1] / 2 - lineWidth / 2;
            box.Add(Rule(lineWidth, 0, -total, ctx.Color), lineX, 0);
        }

        var width = tableWidth;
        if (tagBoxes.Any(t => t is not null))
        {
            var tagX = tableWidth + TagGap * scale;
            var tagWidth = 0.0;
            for (var r = 0; r < cells.Count; r++)
            {
                if (tagBoxes[r] is { } tag)
                {
                    box.Add(tag, tagX, baselines[r]);
                    tagWidth = Math.Max(tagWidth, tag.Width);
                }
            }

            width = tagX + tagWidth;
        }

        box.Width = width;
        var shift = total / 2 + ctx.Axis;
        var centered = new ContainerBox();
        centered.Add(box, 0, -shift);
        centered.Width = width;
        centered.Height = shift;
        centered.Depth = total - shift;
        return centered;
    }
}
