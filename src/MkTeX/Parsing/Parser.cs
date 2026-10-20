using System.Globalization;
using System.Text;

namespace MkTeX.Parsing;

internal sealed record ParseResult(ListNode Root, string? Tag, bool TagParens);

/// <summary>Recursive descent parser for LaTeX math mode (LaTeX, amsmath, amssymb and common extras).</summary>
internal sealed partial class Parser
{
    public const int MaxLength = 10_000;
    public const int MaxDepth = 100;

    private static readonly HashSet<int> DelimiterCodePoints =
    [
        '(', ')', '[', ']', '{', '}', '|', '/', '\\', 0x2016, 0x27E8, 0x27E9, 0x230A, 0x230B, 0x2308, 0x2309,
        0x2191, 0x2193, 0x2195, 0x21D1, 0x21D3, 0x21D5, 0x27EE, 0x27EF, 0x23B0, 0x23B1, 0x231C, 0x231D,
        0x231E, 0x231F, 0x27E6, 0x27E7, 0x2983, 0x2984, 0x27EA, 0x27EB, 0x2223, 0x2225
    ];

    private readonly TokenStream _tokens;
    private int _depth;
    private MathVariant _variant = MathVariant.Default;
    private bool _boldSymbol;
    private string? _tag;
    private bool _tagParens = true;
    private string? _colorModel;

    private Parser(string source, IReadOnlyDictionary<string, string>? macros)
    {
        _tokens = new TokenStream(source, macros);
    }

    public static ParseResult Parse(string source, IReadOnlyDictionary<string, string>? macros = null)
    {
        if (source.Length > MaxLength)
        {
            throw new MathParseException(MathErrorCode.TooLong, $"Expression longer than {MaxLength} characters.", MaxLength);
        }

        var parser = new Parser(source, macros);
        var root = parser.ParseExpression(static t => t.Kind == TokenKind.End);
        return new ParseResult(root, parser._tag, parser._tagParens);
    }

    private ListNode ParseExpression(Func<Token, bool> stop)
    {
        var items = new List<Node>();
        List<Node>? numerator = null;
        Token infix = default;
        while (true)
        {
            var token = _tokens.PeekNonSpace();
            if (stop(token))
            {
                break;
            }

            switch (token.Kind)
            {
                case TokenKind.End:
                    throw new MathParseException(MathErrorCode.UnexpectedEnd, "Unexpected end of input.", token.Position);
                case TokenKind.EndGroup:
                    throw new MathParseException(MathErrorCode.UnbalancedGroup, "Unexpected closing brace.", token.Position);
                case TokenKind.Align:
                    throw new MathParseException(MathErrorCode.UnexpectedToken, "Alignment tab & outside an environment.", token.Position);
            }

            if (token.Kind == TokenKind.Command)
            {
                if (token.Text is "over" or "atop" or "choose" or "brace" or "brack" or "above")
                {
                    if (numerator is not null)
                    {
                        throw new MathParseException(MathErrorCode.UnexpectedToken, "Only one infix fraction per group.", token.Position);
                    }

                    _tokens.Next();
                    numerator = items;
                    items = [];
                    infix = token;
                    if (token.Text == "above")
                    {
                        infix = token with { Text = "above:" + ReadInlineDimension().Em.ToString(CultureInfo.InvariantCulture) };
                    }

                    continue;
                }

                if (TryParseSwitch(token, stop, out var switched))
                {
                    items.Add(switched);
                    break;
                }
            }

            var atom = ParseAtom();
            if (atom is not null)
            {
                items.Add(atom);
            }
        }

        if (numerator is null)
        {
            return new ListNode(items);
        }

        var num = new ListNode(numerator);
        var den = new ListNode(items);
        var name = infix.Text;
        FracNode frac = name switch
        {
            "over" => new FracNode(num, den, null, FracStyle.Auto, 0, 0),
            "atop" => new FracNode(num, den, 0, FracStyle.Auto, 0, 0),
            "choose" => new FracNode(num, den, 0, FracStyle.Auto, '(', ')'),
            "brace" => new FracNode(num, den, 0, FracStyle.Auto, '{', '}'),
            "brack" => new FracNode(num, den, 0, FracStyle.Auto, '[', ']'),
            _ => new FracNode(num, den, double.Parse(name["above:".Length..], CultureInfo.InvariantCulture), FracStyle.Auto, 0, 0)
        };
        return new ListNode([frac]);
    }

    private bool TryParseSwitch(Token token, Func<Token, bool> stop, out Node node)
    {
        node = ListNode.Empty;
        MathStyleKind? style = token.Text switch
        {
            "displaystyle" => MathStyleKind.Display,
            "textstyle" => MathStyleKind.Text,
            "scriptstyle" => MathStyleKind.Script,
            "scriptscriptstyle" => MathStyleKind.ScriptScript,
            _ => null
        };
        if (style is not null)
        {
            _tokens.Next();
            node = new StyleNode(style.Value, ParseExpression(stop));
            return true;
        }

        var size = SizeScale(token.Text);
        if (size is not null)
        {
            _tokens.Next();
            node = new SizeNode(size.Value, ParseExpression(stop));
            return true;
        }

        if (token.Text == "color")
        {
            _tokens.Next();
            var color = ReadColor();
            node = new ColorNode(color, ParseExpression(stop));
            return true;
        }

        MathVariant? variant = token.Text switch
        {
            "rm" => MathVariant.Normal,
            "bf" => MathVariant.Bold,
            "it" => MathVariant.Italic,
            "sf" => MathVariant.SansSerif,
            "tt" => MathVariant.Monospace,
            "cal" => MathVariant.Script,
            "frak" => MathVariant.Fraktur,
            _ => null
        };
        if (variant is not null)
        {
            _tokens.Next();
            var saved = _variant;
            _variant = variant.Value;
            node = ParseExpression(stop);
            _variant = saved;
            return true;
        }

        return false;
    }

    private Node? ParseAtom()
    {
        var nucleus = ParseBase();
        if (nucleus is null)
        {
            return null;
        }

        Node? sup = null;
        Node? sub = null;
        while (true)
        {
            var token = _tokens.PeekNonSpace();
            if (token.Kind == TokenKind.Command && token.Text is "limits" or "nolimits" or "displaylimits")
            {
                _tokens.Next();
                if (nucleus is OpNode op)
                {
                    nucleus = op with { LimitsOverride = token.Text == "displaylimits" ? null : token.Text == "limits" };
                }
                else if (token.Text != "displaylimits")
                {
                    throw new MathParseException(MathErrorCode.UnexpectedToken, $"\\{token.Text} must follow an operator.", token.Position);
                }

                continue;
            }

            if (token.IsChar("'"))
            {
                if (sup is not null)
                {
                    throw new MathParseException(MathErrorCode.DoubleScript, "Double superscript.", token.Position);
                }

                var count = 0;
                while (_tokens.Peek().IsChar("'"))
                {
                    _tokens.Next();
                    count++;
                }

                Node? extra = null;
                if (_tokens.PeekNonSpace().Kind == TokenKind.Superscript)
                {
                    _tokens.Next();
                    extra = ParseArgument();
                }

                sup = new PrimesNode(count, extra);
                continue;
            }

            if (token.Kind == TokenKind.Superscript)
            {
                _tokens.Next();
                if (sup is not null)
                {
                    throw new MathParseException(MathErrorCode.DoubleScript, "Double superscript.", token.Position);
                }

                sup = ParseArgument();
                if (PrimeCount(sup) is > 0 and var primeCount)
                {
                    sup = new PrimesNode(primeCount, null);
                }

                continue;
            }

            if (token.Kind == TokenKind.Subscript)
            {
                _tokens.Next();
                if (sub is not null)
                {
                    throw new MathParseException(MathErrorCode.DoubleScript, "Double subscript.", token.Position);
                }

                sub = ParseArgument();
                continue;
            }

            break;
        }

        if (sup is null && sub is null)
        {
            return nucleus;
        }

        if (nucleus is StackNode { Brace: not BraceKind.None } stack)
        {
            var over = stack.Brace is BraceKind.OverBrace or BraceKind.OverBracket or BraceKind.OverParen;
            if (over && sup is not null && stack.Over is null)
            {
                nucleus = stack with { Over = sup };
                sup = null;
            }
            else if (!over && sub is not null && stack.Under is null)
            {
                nucleus = stack with { Under = sub };
                sub = null;
            }

            if (sup is null && sub is null)
            {
                return nucleus;
            }
        }

        return new ScriptsNode(nucleus, sup, sub);
    }

    // Unicode math fonts draw U+2032 already raised, so ^{\prime} is typeset like x'.
    private static int PrimeCount(Node node) => node switch
    {
        CharNode { CodePoint: 0x2032 } => 1,
        CharNode { CodePoint: 0x2033 } => 2,
        CharNode { CodePoint: 0x2034 } => 3,
        ListNode { Items.Count: > 0 } list when list.Items.All(i => PrimeCount(i) > 0) => list.Items.Sum(PrimeCount),
        _ => 0
    };

    private Node? ParseBase()
    {
        var token = _tokens.PeekNonSpace();
        switch (token.Kind)
        {
            case TokenKind.Superscript:
            case TokenKind.Subscript:
                return ListNode.Empty;
            case TokenKind.Char when token.Text == "'":
                return ListNode.Empty;
        }

        token = _tokens.Next();
        switch (token.Kind)
        {
            case TokenKind.BeginGroup:
                return ParseGroupBody(token);
            case TokenKind.Char:
                return MakeChar(token.Text);
            case TokenKind.Active:
                return new SpaceNode(0.333, false);
            case TokenKind.Command:
                return ParseCommand(token);
            case TokenKind.MathShift:
                throw new MathParseException(MathErrorCode.UnexpectedToken, "Unexpected $ in math mode.", token.Position);
            case TokenKind.Param:
                throw new MathParseException(MathErrorCode.UnexpectedToken, "Unexpected # outside a macro.", token.Position);
            case TokenKind.End:
                throw new MathParseException(MathErrorCode.UnexpectedEnd, "Unexpected end of input.", token.Position);
            default:
                throw new MathParseException(MathErrorCode.UnexpectedToken, $"Unexpected '{token}'.", token.Position);
        }
    }

    private ListNode ParseGroupBody(Token open)
    {
        Enter(open.Position);
        var savedVariant = _variant;
        var savedBold = _boldSymbol;
        var list = ParseExpression(static t => t.Kind is TokenKind.EndGroup or TokenKind.End);
        _variant = savedVariant;
        _boldSymbol = savedBold;
        var close = _tokens.Next();
        if (close.Kind != TokenKind.EndGroup)
        {
            throw new MathParseException(MathErrorCode.UnbalancedGroup, "Missing closing brace.", open.Position);
        }

        _depth--;
        return list;
    }

    private Node ParseArgument()
    {
        _tokens.SkipSpaces();
        var token = _tokens.Next();
        switch (token.Kind)
        {
            case TokenKind.BeginGroup:
                return ParseGroupBody(token);
            case TokenKind.End:
            case TokenKind.EndGroup:
            case TokenKind.Align:
            case TokenKind.Superscript:
            case TokenKind.Subscript:
                throw new MathParseException(MathErrorCode.MissingArgument, "Missing argument.", token.Position);
        }

        _tokens.PushBack(token);
        Enter(token.Position);
        var node = ParseBase() ?? ListNode.Empty;
        _depth--;
        return node;
    }

    private Node ParseArgument(MathVariant variant, bool boldSymbol = false)
    {
        var savedVariant = _variant;
        var savedBold = _boldSymbol;
        _variant = variant;
        _boldSymbol = boldSymbol;
        var node = ParseArgument();
        _variant = savedVariant;
        _boldSymbol = savedBold;
        return node;
    }

    private Node? ParseOptionalArgument()
    {
        var raw = _tokens.ReadRawOptional();
        if (raw is null)
        {
            return null;
        }

        return ParseTokens(raw);
    }

    private Node ParseTokens(List<Token> tokens)
    {
        var position = tokens.Count > 0 ? tokens[0].Position : 0;
        _tokens.PushBack([new Token(TokenKind.BeginGroup, "{", position), .. tokens, new Token(TokenKind.EndGroup, "}", position)]);
        return ParseArgument();
    }

    private void Enter(int position)
    {
        if (++_depth > MaxDepth)
        {
            throw new MathParseException(MathErrorCode.TooDeep, $"Nesting deeper than {MaxDepth} levels.", position);
        }
    }

    private CharNode MakeChar(string text)
    {
        var cp = char.ConvertToUtf32(text, 0);
        switch (cp)
        {
            case '-':
                return new CharNode(0x2212, AtomType.Bin, MathVariant.Normal);
            case '*':
                return new CharNode(0x2217, AtomType.Bin, MathVariant.Normal);
            case '`':
                return new CharNode(0x2018, AtomType.Ord, MathVariant.Normal);
            case '"':
                return new CharNode(0x201D, AtomType.Ord, MathVariant.Normal);
        }

        return Char(cp, Symbols.TypeOf(cp));
    }

    private CharNode Char(int cp, AtomType type)
    {
        var variant = _boldSymbol ? Alphabets.BoldSymbol(cp) : _variant;
        return new CharNode(cp, type, variant);
    }

    private string ReadString() => string.Concat(_tokens.ReadRawArgument().Select(t => t.Kind == TokenKind.Command ? "\\" + t.Text : t.Text)).Trim();

    /// <summary>Reads [model]{color}; the model is remembered for a following color of the same command.</summary>
    private string ReadColor(bool inheritModel = false)
    {
        var position = _tokens.PeekRaw().Position;
        var model = _tokens.ReadRawOptional();
        if (model is not null)
        {
            _colorModel = string.Concat(model.Select(t => t.Text)).Trim();
        }
        else if (!inheritModel)
        {
            _colorModel = null;
        }

        var text = ReadString();
        if (_colorModel is not null && _colorModel.Equals("HTML", StringComparison.OrdinalIgnoreCase) && !text.StartsWith('#'))
        {
            text = "#" + text;
        }
        else if (_colorModel is "RGB")
        {
            var parts = text.Split(',');
            if (parts.All(p => double.TryParse(p.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
            {
                text = string.Join(',', parts.Select(p => (double.Parse(p.Trim(), CultureInfo.InvariantCulture) / 255).ToString(CultureInfo.InvariantCulture)));
            }
        }

        return Colors.Parse(text, position);
    }

    private static double? SizeScale(string name) => name switch
    {
        "tiny" => 0.5,
        "scriptsize" => 0.7,
        "footnotesize" => 0.8,
        "small" => 0.9,
        "normalsize" => 1,
        "large" => 1.2,
        "Large" => 1.44,
        "LARGE" => 1.728,
        "huge" => 2.074,
        "Huge" => 2.488,
        _ => null
    };

    private int ReadDelimiter()
    {
        _tokens.SkipSpaces();
        var token = _tokens.Next();
        if (token.Kind == TokenKind.Char)
        {
            switch (token.Text)
            {
                case ".":
                    return 0;
                case "<":
                    return 0x27E8;
                case ">":
                    return 0x27E9;
            }

            var cp = char.ConvertToUtf32(token.Text, 0);
            if (DelimiterCodePoints.Contains(cp))
            {
                return cp;
            }
        }
        else if (token.Kind == TokenKind.Command)
        {
            switch (token.Text)
            {
                case "{":
                    return '{';
                case "}":
                    return '}';
                case "|":
                    return 0x2016;
                case "backslash":
                    return '\\';
            }

            if (Symbols.TryGet(token.Text, out var symbol) && DelimiterCodePoints.Contains(symbol.CodePoint))
            {
                return symbol.CodePoint;
            }
        }

        throw new MathParseException(MathErrorCode.InvalidDelimiter, $"'{token}' is not a delimiter.", token.Position);
    }

    private (double Em, bool Mu) ReadInlineDimension()
    {
        _tokens.SkipSpaces();
        var number = new StringBuilder();
        var position = _tokens.PeekRaw().Position;
        while (true)
        {
            var token = _tokens.Next();
            if (token.Kind == TokenKind.Char && token.Text is [var c] && (char.IsAsciiDigit(c) || c is '.' or ',' or '-' or '+'))
            {
                number.Append(c == ',' ? '.' : c);
                continue;
            }

            if (token.Kind == TokenKind.Space && number.Length > 0)
            {
                continue;
            }

            _tokens.PushBack(token);
            break;
        }

        var unit = new StringBuilder();
        while (unit.Length < 2)
        {
            var token = _tokens.Next();
            if (token.Kind == TokenKind.Char && token.Text is [var c] && char.IsAsciiLetter(c))
            {
                unit.Append(c);
                continue;
            }

            _tokens.PushBack(token);
            break;
        }

        return ParseDimension(number + unit.ToString(), position);
    }

    private (double Em, bool Mu) ReadDimensionArgument()
    {
        var position = _tokens.PeekRaw().Position;
        return ParseDimension(ReadString(), position);
    }

    internal static (double Em, bool Mu) ParseDimension(string text, int position)
    {
        text = text.Replace(" ", "", StringComparison.Ordinal).Replace(',', '.');
        var split = 0;
        while (split < text.Length && (char.IsAsciiDigit(text[split]) || text[split] is '.' or '-' or '+'))
        {
            split++;
        }

        var numberText = text[..split];
        var unit = text[split..];
        var sign = 1.0;
        while (numberText.StartsWith('-') || numberText.StartsWith('+'))
        {
            if (numberText[0] == '-')
            {
                sign = -sign;
            }

            numberText = numberText[1..];
        }

        if (numberText.Length == 0)
        {
            numberText = "1";
        }

        if (!double.TryParse(numberText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            throw new MathParseException(MathErrorCode.InvalidArgument, $"Invalid dimension '{text}'.", position);
        }

        value *= sign;
        double factor = unit switch
        {
            "em" => 1,
            "ex" => 0.431,
            "mu" => 1,
            "pt" => 0.1,
            "pc" => 1.2,
            "in" => 7.227,
            "bp" or "px" => 0.100375,
            "cm" => 2.84527,
            "mm" => 0.284527,
            "dd" => 0.107,
            "cc" => 1.284,
            "sp" => 0.1 / 65536,
            _ => throw new MathParseException(MathErrorCode.InvalidArgument, $"Invalid unit in '{text}'.", position)
        };
        return (value * factor, unit == "mu");
    }
}
