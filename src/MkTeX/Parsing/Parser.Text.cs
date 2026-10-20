using System.Text;

namespace MkTeX.Parsing;

internal sealed partial class Parser
{
    private static readonly Dictionary<string, string> TextSymbols = new(StringComparer.Ordinal)
    {
        ["{"] = "{", ["}"] = "}", ["$"] = "$", ["%"] = "%", ["&"] = "&", ["_"] = "_", ["#"] = "#",
        ["textbackslash"] = "\\", ["textasciitilde"] = "~", ["textasciicircum"] = "^", ["textbar"] = "|",
        ["textless"] = "<", ["textgreater"] = ">", ["textendash"] = "\u2013", ["textemdash"] = "\u2014",
        ["textquoteleft"] = "\u2018", ["textquoteright"] = "\u2019", ["textquotedblleft"] = "\u201C",
        ["textquotedblright"] = "\u201D", ["textbullet"] = "\u2022", ["textdagger"] = "\u2020",
        ["textdaggerdbl"] = "\u2021", ["textdegree"] = "\u00B0", ["textellipsis"] = "\u2026", ["ldots"] = "\u2026",
        ["dots"] = "\u2026", ["AA"] = "\u00C5", ["aa"] = "\u00E5", ["AE"] = "\u00C6", ["ae"] = "\u00E6",
        ["OE"] = "\u0152", ["oe"] = "\u0153", ["O"] = "\u00D8", ["o"] = "\u00F8", ["ss"] = "\u00DF",
        ["i"] = "\u0131", ["j"] = "\u0237", ["L"] = "\u0141", ["l"] = "\u0142", ["S"] = "\u00A7", ["P"] = "\u00B6",
        ["copyright"] = "\u00A9", ["textregistered"] = "\u00AE", ["pounds"] = "\u00A3", ["euro"] = "\u20AC",
        ["textsection"] = "\u00A7", ["textunderscore"] = "_", ["slash"] = "/"
    };

    private static readonly Dictionary<string, char> TextAccents = new(StringComparer.Ordinal)
    {
        ["'"] = '\u0301', ["`"] = '\u0300', ["^"] = '\u0302', ["\""] = '\u0308', ["~"] = '\u0303', ["="] = '\u0304',
        ["."] = '\u0307', ["u"] = '\u0306', ["v"] = '\u030C', ["H"] = '\u030B', ["c"] = '\u0327', ["r"] = '\u030A',
        ["k"] = '\u0328', ["b"] = '\u0331', ["d"] = '\u0323'
    };

    private Node ParseTextArgument(MathVariant variant)
    {
        _tokens.SkipSpaces();
        var open = _tokens.Next();
        if (open.Kind != TokenKind.BeginGroup)
        {
            if (open.Kind is TokenKind.End or TokenKind.EndGroup)
            {
                throw new MathParseException(MathErrorCode.MissingArgument, "Missing argument.", open.Position);
            }

            _tokens.PushBack(new Token(TokenKind.EndGroup, "}", open.Position));
            _tokens.PushBack(open);
        }

        Enter(open.Position);
        var node = ParseTextUntilGroupEnd(variant, open.Position);
        _depth--;
        return node;
    }

    private ListNode ParseTextUntilGroupEnd(MathVariant variant, int start)
    {
        var items = new List<Node>();
        var text = new StringBuilder();

        void Flush()
        {
            if (text.Length > 0)
            {
                items.Add(new TextNode(text.ToString(), variant));
                text.Clear();
            }
        }

        while (true)
        {
            var token = _tokens.Next();
            switch (token.Kind)
            {
                case TokenKind.End:
                    throw new MathParseException(MathErrorCode.UnbalancedGroup, "Missing closing brace.", start);
                case TokenKind.EndGroup:
                    Flush();
                    return new ListNode(items);
                case TokenKind.BeginGroup:
                    Flush();
                    Enter(token.Position);
                    items.Add(ParseTextUntilGroupEnd(variant, token.Position));
                    _depth--;
                    continue;
                case TokenKind.Space:
                    if (text.Length == 0 || text[^1] != ' ')
                    {
                        text.Append(' ');
                    }

                    continue;
                case TokenKind.Active:
                    text.Append('\u00A0');
                    continue;
                case TokenKind.MathShift:
                    Flush();
                    var savedVariant = _variant;
                    _variant = MathVariant.Default;
                    items.Add(ParseExpression(static t => t.Kind is TokenKind.MathShift or TokenKind.End));
                    _variant = savedVariant;
                    if (_tokens.Next().Kind != TokenKind.MathShift)
                    {
                        throw new MathParseException(MathErrorCode.UnexpectedEnd, "Missing closing $.", token.Position);
                    }

                    continue;
                case TokenKind.Command:
                    break;
                default:
                    text.Append(token.Text);
                    continue;
            }

            var name = token.Text;
            if (TextSymbols.TryGetValue(name, out var literal))
            {
                text.Append(literal);
                continue;
            }

            if (TextAccents.TryGetValue(name, out var combining))
            {
                var target = _tokens.ReadRawArgument();
                var baseText = string.Concat(target.Where(t => t.Kind == TokenKind.Char).Select(t => t.Text));
                if (target.Count == 1 && target[0].Kind == TokenKind.Command && TextSymbols.TryGetValue(target[0].Text, out var sym))
                {
                    baseText = sym;
                }

                text.Append((baseText + combining).Normalize(NormalizationForm.FormC));
                continue;
            }

            switch (name)
            {
                case " ":
                    text.Append(' ');
                    continue;
                case "\\" or "newline":
                    _tokens.ReadRawOptional();
                    Flush();
                    items.Add(new LineBreakNode());
                    continue;
                case "textbf":
                    Flush();
                    items.Add(ParseTextArgument(Combine(variant, MathVariant.Bold)));
                    continue;
                case "textit" or "emph" or "textsl":
                    Flush();
                    items.Add(ParseTextArgument(Combine(variant, MathVariant.Italic)));
                    continue;
                case "textsf":
                    Flush();
                    items.Add(ParseTextArgument(MathVariant.SansSerif));
                    continue;
                case "texttt":
                    Flush();
                    items.Add(ParseTextArgument(MathVariant.Monospace));
                    continue;
                case "text" or "textrm" or "textnormal" or "textup" or "textmd" or "mbox" or "hbox":
                    Flush();
                    items.Add(ParseTextArgument(variant));
                    continue;
                case "bfseries" or "bf":
                    Flush();
                    items.Add(ParseTextUntilGroupEnd(Combine(variant, MathVariant.Bold), token.Position));
                    _tokens.PushBack(new Token(TokenKind.EndGroup, "}", token.Position));
                    continue;
                case "itshape" or "it":
                    Flush();
                    items.Add(ParseTextUntilGroupEnd(Combine(variant, MathVariant.Italic), token.Position));
                    _tokens.PushBack(new Token(TokenKind.EndGroup, "}", token.Position));
                    continue;
                case "textcolor":
                {
                    Flush();
                    var color = ReadColor();
                    items.Add(new ColorNode(color, ParseTextArgument(variant)));
                    continue;
                }
                case "color":
                {
                    Flush();
                    var color = ReadColor();
                    items.Add(new ColorNode(color, ParseTextUntilGroupEnd(variant, token.Position)));
                    _tokens.PushBack(new Token(TokenKind.EndGroup, "}", token.Position));
                    continue;
                }
                case "kern" or "hskip":
                    Flush();
                    items.Add(new SpaceNode(ReadInlineDimension().Em, false));
                    continue;
                case "hspace" or "hspace*":
                    Flush();
                    if (_tokens.Peek().IsChar("*"))
                    {
                        _tokens.Next();
                    }

                    items.Add(new SpaceNode(ReadDimensionArgument().Em, false));
                    continue;
                case "quad":
                    Flush();
                    items.Add(new SpaceNode(1, false));
                    continue;
                case "qquad":
                    Flush();
                    items.Add(new SpaceNode(2, false));
                    continue;
                case "," or "thinspace":
                    Flush();
                    items.Add(new SpaceNode(0.1667, false));
                    continue;
                case "raisebox":
                {
                    Flush();
                    var amount = ReadDimensionArgument().Em;
                    items.Add(new RaiseNode(amount, ParseTextArgument(variant)));
                    continue;
                }
                case "relax" or "nobreak" or "allowbreak" or "protect":
                    continue;
                case "verb":
                    throw new MathParseException(MathErrorCode.UnknownCommand, "\\verb is not supported.", token.Position);
            }

            var scale = SizeScale(name);
            if (scale is not null)
            {
                Flush();
                items.Add(new SizeNode(scale.Value, ParseTextUntilGroupEnd(variant, token.Position)));
                _tokens.PushBack(new Token(TokenKind.EndGroup, "}", token.Position));
                continue;
            }

            if (Symbols.TryGet(name, out var symbol) && symbol.Type != AtomType.Op)
            {
                text.Append(char.ConvertFromUtf32(symbol.CodePoint));
                continue;
            }

            throw new MathParseException(MathErrorCode.UnknownCommand, $"Unknown command \\{name} in text.", token.Position);
        }
    }

    private static MathVariant Combine(MathVariant current, MathVariant added) => (current, added) switch
    {
        (MathVariant.Bold, MathVariant.Italic) or (MathVariant.Italic, MathVariant.Bold) => MathVariant.BoldItalic,
        (MathVariant.BoldItalic, _) => MathVariant.BoldItalic,
        (MathVariant.SansSerif, MathVariant.Bold) => MathVariant.SansSerifBold,
        (MathVariant.SansSerif, MathVariant.Italic) => MathVariant.SansSerifItalic,
        _ => added
    };
}
