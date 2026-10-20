namespace MkTeX.Parsing;

internal sealed partial class Parser
{
    private sealed record ArraySpec(
        List<ColumnAlign> Columns,
        List<bool> Lines,
        double ColumnSpacing = 1,
        double RowSpacing = 1,
        bool AlignPairs = false,
        MathStyleKind? CellStyle = MathStyleKind.Text,
        int Left = 0,
        int Right = 0,
        ColumnAlign Default = ColumnAlign.Center);

    private Node ParseEnvironment(Token begin)
    {
        var name = ReadString();
        var spec = name switch
        {
            "matrix" or "matrix*" => Matrix(0, 0),
            "pmatrix" or "pmatrix*" => Matrix('(', ')'),
            "bmatrix" or "bmatrix*" => Matrix('[', ']'),
            "Bmatrix" or "Bmatrix*" => Matrix('{', '}'),
            "vmatrix" or "vmatrix*" => Matrix('|', '|'),
            "Vmatrix" or "Vmatrix*" => Matrix(0x2016, 0x2016),
            "smallmatrix" => new ArraySpec([], [], 0.5556, 0.7, CellStyle: MathStyleKind.Script),
            "cases" => Cases(MathStyleKind.Text, '{', 0),
            "dcases" => Cases(MathStyleKind.Display, '{', 0),
            "rcases" => Cases(MathStyleKind.Text, 0, '}'),
            "drcases" => Cases(MathStyleKind.Display, 0, '}'),
            "array" or "darray" => ParseColumnSpec(name == "darray" ? MathStyleKind.Display : MathStyleKind.Text),
            "subarray" => ParseColumnSpec(MathStyleKind.Script) with { ColumnSpacing = 0, RowSpacing = 0.7 },
            "aligned" or "align" or "align*" or "split" or "eqnarray" or "eqnarray*" or "flalign" or "flalign*" =>
                new ArraySpec([], [], 1, 1.25, AlignPairs: true, CellStyle: MathStyleKind.Display),
            "alignat" or "alignat*" or "alignedat" => SkipArgument(new ArraySpec([], [], 0, 1.25, AlignPairs: true, CellStyle: MathStyleKind.Display)),
            "gather" or "gather*" or "gathered" or "multline" or "multline*" =>
                new ArraySpec([], [], 0, 1.25, CellStyle: MathStyleKind.Display),
            "equation" or "equation*" => new ArraySpec([], [], 0, 1, CellStyle: MathStyleKind.Display),
            _ => throw new MathParseException(MathErrorCode.UnknownEnvironment, $"Unknown environment '{name}'.", begin.Position)
        };

        if (name.EndsWith("matrix*", StringComparison.Ordinal))
        {
            var align = _tokens.ReadRawOptional();
            if (align is not null)
            {
                var text = string.Concat(align.Select(t => t.Text)).Trim();
                spec = spec with { Default = text == "l" ? ColumnAlign.Left : text == "r" ? ColumnAlign.Right : ColumnAlign.Center };
            }
        }

        var array = ParseArrayBody(spec, name, begin.Position);
        if (spec.Left == 0 && spec.Right == 0)
        {
            return array;
        }

        return new LeftRightNode(spec.Left, spec.Right, [array], []);
    }

    private static ArraySpec Matrix(int left, int right) => new([], [], 1, 1, Left: left, Right: right);

    private static ArraySpec Cases(MathStyleKind style, int left, int right) =>
        new([ColumnAlign.Left, ColumnAlign.Left], [], 1, 1.2, CellStyle: style, Left: left, Right: right, Default: ColumnAlign.Left);

    private ArraySpec SkipArgument(ArraySpec spec)
    {
        _tokens.ReadRawArgument();
        return spec;
    }

    private ArraySpec ParseColumnSpec(MathStyleKind style)
    {
        var tokens = _tokens.ReadRawArgument();
        var columns = new List<ColumnAlign>();
        var lines = new List<bool> { false };
        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Kind == TokenKind.Space)
            {
                continue;
            }

            switch (t.Text)
            {
                case "l":
                    columns.Add(ColumnAlign.Left);
                    lines.Add(false);
                    break;
                case "c":
                    columns.Add(ColumnAlign.Center);
                    lines.Add(false);
                    break;
                case "r":
                    columns.Add(ColumnAlign.Right);
                    lines.Add(false);
                    break;
                case "|" or ":":
                    lines[^1] = true;
                    break;
                case "p" or "m" or "b" or "@" or "*" or "!" or ">" or "<":
                    if (t.Text is "p" or "m" or "b")
                    {
                        columns.Add(ColumnAlign.Left);
                        lines.Add(false);
                    }

                    if (i + 1 < tokens.Count && tokens[i + 1].Kind == TokenKind.BeginGroup)
                    {
                        var depth = 0;
                        do
                        {
                            i++;
                            if (tokens[i].Kind == TokenKind.BeginGroup)
                            {
                                depth++;
                            }
                            else if (tokens[i].Kind == TokenKind.EndGroup)
                            {
                                depth--;
                            }
                        }
                        while (depth > 0 && i + 1 < tokens.Count);
                    }

                    break;
                default:
                    throw new MathParseException(MathErrorCode.InvalidArgument, $"Invalid column specifier '{t}'.", t.Position);
            }
        }

        return new ArraySpec(columns, lines, 1, 1, CellStyle: style);
    }

    private ArrayNode ParseArrayBody(ArraySpec spec, string? environment, int position)
    {
        Enter(position);
        var rows = new List<IReadOnlyList<Node>>();
        var rowLines = new List<bool> { SkipHorizontalLines() };
        var tags = new List<string?>();
        var row = new List<Node>();
        var savedTag = _tag;
        var savedParens = _tagParens;
        _tag = null;

        static bool IsCellEnd(Token t) =>
            t.Kind is TokenKind.Align or TokenKind.End or TokenKind.EndGroup ||
            t.IsCommand("\\") || t.IsCommand("cr") || t.IsCommand("tabularnewline") || t.IsCommand("end");

        while (true)
        {
            var cell = ParseExpression(IsCellEnd);
            if (spec.AlignPairs && row.Count % 2 == 1)
            {
                cell = new ListNode([ListNode.Empty, .. cell.Items]);
            }

            row.Add(cell);
            var token = _tokens.Next();
            if (token.Kind == TokenKind.Align)
            {
                continue;
            }

            if (token.Kind == TokenKind.Command && token.Text is "\\" or "cr" or "tabularnewline")
            {
                _tokens.ReadRawOptional();
                rows.Add(row);
                tags.Add(_tag is null ? null : _tagParens ? $"({_tag})" : _tag);
                _tag = null;
                row = [];
                rowLines.Add(SkipHorizontalLines());
                continue;
            }

            if (environment is null)
            {
                if (token.Kind != TokenKind.EndGroup)
                {
                    throw new MathParseException(MathErrorCode.UnbalancedGroup, "Missing closing brace.", position);
                }

                _tokens.PushBack(token);
            }
            else
            {
                if (!token.IsCommand("end"))
                {
                    throw new MathParseException(MathErrorCode.MismatchedEnvironment, $"Missing \\end{{{environment}}}.", position);
                }

                var endName = ReadString();
                if (endName != environment)
                {
                    throw new MathParseException(MathErrorCode.MismatchedEnvironment, $"\\begin{{{environment}}} ended by \\end{{{endName}}}.", token.Position);
                }
            }

            var trailingEmpty = row.Count == 1 && row[0] is ListNode { Items.Count: 0 } && rows.Count > 0;
            if (!trailingEmpty)
            {
                rows.Add(row);
                tags.Add(_tag is null ? null : _tagParens ? $"({_tag})" : _tag);
            }

            break;
        }

        _tag = savedTag;
        _tagParens = savedParens;
        _depth--;

        while (rowLines.Count < rows.Count + 1)
        {
            rowLines.Add(false);
        }

        var columnCount = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        var columns = new List<ColumnAlign>(spec.Columns);
        for (var i = columns.Count; i < columnCount; i++)
        {
            columns.Add(spec.AlignPairs ? (i % 2 == 0 ? ColumnAlign.Right : ColumnAlign.Left) : spec.Default);
        }

        var columnLines = new List<bool>(spec.Lines);
        while (columnLines.Count < columns.Count + 1)
        {
            columnLines.Add(false);
        }

        return new ArrayNode(rows, columns, columnLines, rowLines, spec.ColumnSpacing, spec.RowSpacing, spec.AlignPairs, spec.CellStyle, tags);
    }

    private bool SkipHorizontalLines()
    {
        var found = false;
        while (true)
        {
            var token = _tokens.PeekNonSpace();
            if (token.IsCommand("hline") || token.IsCommand("hdashline"))
            {
                _tokens.Next();
                found = true;
                continue;
            }

            return found;
        }
    }

    private ArrayNode ParseSubstack(Token token)
    {
        _tokens.SkipSpaces();
        var open = _tokens.Next();
        if (open.Kind != TokenKind.BeginGroup)
        {
            throw new MathParseException(MathErrorCode.MissingArgument, "\\substack expects a group.", token.Position);
        }

        var spec = new ArraySpec([ColumnAlign.Center], [], 0, 0.7, CellStyle: MathStyleKind.Script);
        var array = ParseArrayBody(spec, null, token.Position);
        _tokens.Next();
        return array;
    }
}
