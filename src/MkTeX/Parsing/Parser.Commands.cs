using System.Globalization;

namespace MkTeX.Parsing;

internal sealed partial class Parser
{
    private Node? ParseCommand(Token token)
    {
        var name = token.Text;
        if (Symbols.TryGet(name, out var symbol))
        {
            return symbol.Type == AtomType.Op
                ? new OpNode(null, symbol.CodePoint, symbol.Limits)
                : Char(symbol.CodePoint, symbol.Type);
        }

        if (Symbols.Operators.TryGetValue(name, out var op))
        {
            return new OpNode(new TextNode(op.Text, MathVariant.Normal), 0, op.Limits);
        }

        if (Symbols.Accents.TryGetValue(name, out var accent))
        {
            return new AccentNode(ParseArgument(), accent.Accent, accent.Wide, accent.Under, accent.Stretchy);
        }

        switch (name)
        {
            case "{": return Char('{', AtomType.Open);
            case "}": return Char('}', AtomType.Close);
            case "_" or "&" or "#" or "$" or "%":
                return Char(name[0], AtomType.Ord);
            case ",": return new SpaceNode(3, true);
            case ":" or ">": return new SpaceNode(4, true);
            case ";": return new SpaceNode(5, true);
            case "!": return new SpaceNode(-3, true);
            case " ": return new SpaceNode(0.333, false);
            case "/": return new SpaceNode(0, false);
            case "\\" or "newline" or "cr":
                _tokens.ReadRawOptional();
                return new LineBreakNode();
            case "frac": return new FracNode(ParseArgument(), ParseArgument(), null, FracStyle.Auto, 0, 0);
            case "cfrac":
                _tokens.ReadRawOptional();
                return new FracNode(ParseArgument(), ParseArgument(), null, FracStyle.Display, 0, 0, Continued: true);
            case "genfrac": return ParseGenfrac();
            case "sqrt":
            {
                var degree = ParseOptionalArgument();
                return new RadicalNode(ParseArgument(), degree);
            }
            case "left": return ParseLeftRight(token);
            case "right" or "middle":
                throw new MathParseException(MathErrorCode.MissingDelimiter, $"\\{name} without matching \\left.", token.Position);
            case "big" or "Big" or "bigg" or "Bigg" or "bigl" or "Bigl" or "biggl" or "Biggl" or "bigr" or "Bigr" or
                "biggr" or "Biggr" or "bigm" or "Bigm" or "biggm" or "Biggm":
                return ParseBigDelimiter(name);
            case "overline": return new BarNode(ParseArgument(), Over: true);
            case "underline" or "underbar": return new BarNode(ParseArgument(), Over: false);
            case "overbrace": return new StackNode(ParseArgument(), null, null, BraceKind.OverBrace);
            case "underbrace": return new StackNode(ParseArgument(), null, null, BraceKind.UnderBrace);
            case "overbracket": return new StackNode(ParseArgument(), null, null, BraceKind.OverBracket);
            case "underbracket": return new StackNode(ParseArgument(), null, null, BraceKind.UnderBracket);
            case "overparen": return new StackNode(ParseArgument(), null, null, BraceKind.OverParen);
            case "underparen": return new StackNode(ParseArgument(), null, null, BraceKind.UnderParen);
            case "overset":
            {
                var over = ParseArgument();
                var body = ParseArgument();
                return new ClassNode(ClassOf(body), new StackNode(body, over, null, BraceKind.None));
            }
            case "underset":
            {
                var under = ParseArgument();
                var body = ParseArgument();
                return new ClassNode(ClassOf(body), new StackNode(body, null, under, BraceKind.None));
            }
            case "stackrel":
            {
                var over = ParseArgument();
                return new ClassNode(AtomType.Rel, new StackNode(ParseArgument(), over, null, BraceKind.None));
            }
            case "overunderset":
            {
                var over = ParseArgument();
                var under = ParseArgument();
                var body = ParseArgument();
                return new ClassNode(ClassOf(body), new StackNode(body, over, under, BraceKind.None));
            }
            case "xrightarrow": return ParseExtensibleArrow(BraceKind.ArrowRight);
            case "xleftarrow": return ParseExtensibleArrow(BraceKind.ArrowLeft);
            case "xleftrightarrow": return ParseExtensibleArrow(BraceKind.ArrowLeftRight);
            case "xRightarrow": return ParseExtensibleArrow(BraceKind.DoubleRight);
            case "xLeftarrow": return ParseExtensibleArrow(BraceKind.DoubleLeft);
            case "xLeftrightarrow": return ParseExtensibleArrow(BraceKind.DoubleLeftRight);
            case "xhookrightarrow": return ParseExtensibleArrow(BraceKind.HookRight);
            case "xhookleftarrow": return ParseExtensibleArrow(BraceKind.HookLeft);
            case "xmapsto": return ParseExtensibleArrow(BraceKind.MapsTo);
            case "xrightleftharpoons" or "xleftrightharpoons": return ParseExtensibleArrow(BraceKind.Harpoons);
            case "xlongequal": return ParseExtensibleArrow(BraceKind.Equals);
            case "operatorname" or "operatorname*" or "operatornamewithlimits":
            {
                var limits = name != "operatorname";
                if (name == "operatorname" && _tokens.Peek().IsChar("*"))
                {
                    _tokens.Next();
                    limits = true;
                }

                return new OpNode(ParseArgument(MathVariant.Normal), 0, limits);
            }
            case "mathop": return new OpNode(ParseArgument(), 0, true);
            case "mathord": return new ClassNode(AtomType.Ord, ParseArgument());
            case "mathbin": return new ClassNode(AtomType.Bin, ParseArgument());
            case "mathrel": return new ClassNode(AtomType.Rel, ParseArgument());
            case "mathopen": return new ClassNode(AtomType.Open, ParseArgument());
            case "mathclose": return new ClassNode(AtomType.Close, ParseArgument());
            case "mathpunct": return new ClassNode(AtomType.Punct, ParseArgument());
            case "mathinner": return new ClassNode(AtomType.Inner, ParseArgument());
            case "mathrm" or "mathup": return ParseArgument(MathVariant.Normal);
            case "mathnormal": return ParseArgument(MathVariant.Default);
            case "mathit": return ParseArgument(MathVariant.Italic);
            case "mathbf": return ParseArgument(MathVariant.Bold);
            case "mathsf": return ParseArgument(MathVariant.SansSerif);
            case "mathsfit": return ParseArgument(MathVariant.SansSerifItalic);
            case "mathtt": return ParseArgument(MathVariant.Monospace);
            case "mathcal" or "mathscr": return ParseArgument(MathVariant.Script);
            case "mathfrak": return ParseArgument(MathVariant.Fraktur);
            case "mathbb": return ParseArgument(MathVariant.DoubleStruck);
            case "boldsymbol" or "pmb": return ParseArgument(MathVariant.BoldItalic, boldSymbol: true);
            case "text" or "textrm" or "textnormal" or "textup" or "textmd" or "mbox" or "hbox" or "textsl":
                return ParseTextArgument(MathVariant.Normal);
            case "textbf": return ParseTextArgument(MathVariant.Bold);
            case "textit" or "emph": return ParseTextArgument(MathVariant.Italic);
            case "textsf": return ParseTextArgument(MathVariant.SansSerif);
            case "texttt": return ParseTextArgument(MathVariant.Monospace);
            case "textcolor":
            {
                var color = ReadColor();
                return new ColorNode(color, ParseArgument());
            }
            case "colorbox":
            {
                var color = ReadColor();
                return new BoxNode(ParseTextArgument(MathVariant.Normal), BoxKind.Color, color, null, 0.3);
            }
            case "fcolorbox":
            {
                var border = ReadColor();
                var background = ReadColor(inheritModel: true);
                return new BoxNode(ParseTextArgument(MathVariant.Normal), BoxKind.FrameColor, background, border, 0.3);
            }
            case "boxed": return new BoxNode(new StyleNode(MathStyleKind.Display, ParseArgument()), BoxKind.Frame, null, null, 0.3);
            case "fbox": return new BoxNode(ParseTextArgument(MathVariant.Normal), BoxKind.Frame, null, null, 0.3);
            case "phantom": return new PhantomNode(ParseArgument(), true, true);
            case "hphantom": return new PhantomNode(ParseArgument(), true, false);
            case "vphantom": return new PhantomNode(ParseArgument(), false, true);
            case "cancel": return new CancelNode(ParseArgument(), CancelKind.Forward);
            case "bcancel": return new CancelNode(ParseArgument(), CancelKind.Back);
            case "xcancel": return new CancelNode(ParseArgument(), CancelKind.Cross);
            case "sout": return new CancelNode(ParseTextArgument(MathVariant.Normal), CancelKind.Strike);
            case "cancelto":
            {
                var to = ParseArgument();
                return new ScriptsNode(new CancelNode(ParseArgument(), CancelKind.Forward), to, null);
            }
            case "llap": return new LapNode(ParseTextArgument(MathVariant.Normal), LapKind.Left);
            case "rlap": return new LapNode(ParseTextArgument(MathVariant.Normal), LapKind.Right);
            case "clap": return new LapNode(ParseTextArgument(MathVariant.Normal), LapKind.Center);
            case "mathllap": return new LapNode(ParseArgument(), LapKind.Left);
            case "mathrlap": return new LapNode(ParseArgument(), LapKind.Right);
            case "mathclap": return new LapNode(ParseArgument(), LapKind.Center);
            case "smash":
            {
                var which = _tokens.ReadRawOptional();
                var text = which is null ? "tb" : string.Concat(which.Select(t => t.Text));
                return new SmashNode(ParseArgument(), text.Contains('t'), text.Contains('b'));
            }
            case "rule":
            {
                var raise = _tokens.ReadRawOptional();
                var raiseEm = raise is null ? 0 : ParseDimension(string.Concat(raise.Select(t => t.Text)), token.Position).Em;
                var width = ReadDimensionArgument().Em;
                var height = ReadDimensionArgument().Em;
                return new RuleNode(width, height, raiseEm);
            }
            case "raisebox":
            {
                var amount = ReadDimensionArgument().Em;
                return new RaiseNode(amount, ParseTextArgument(MathVariant.Normal));
            }
            case "hspace" or "hspace*" or "mspace":
            {
                if (_tokens.Peek().IsChar("*"))
                {
                    _tokens.Next();
                }

                var (em, mu) = ReadDimensionArgument();
                return new SpaceNode(em, mu);
            }
            case "kern" or "mkern" or "hskip" or "mskip":
            {
                var (em, mu) = ReadInlineDimension();
                return new SpaceNode(em, mu);
            }
            case "not": return ParseNot(token);
            case "tag" or "tag*":
            {
                var parens = name == "tag";
                if (_tokens.Peek().IsChar("*"))
                {
                    _tokens.Next();
                    parens = false;
                }

                _tag = ReadString();
                _tagParens = parens;
                return null;
            }
            case "notag":
                _tag = null;
                return null;
            case "def" or "gdef" or "xdef" or "edef":
                _tokens.Define(token.Position);
                return null;
            case "let":
            {
                var target = _tokens.NextRaw();
                _tokens.SkipSpaces();
                var source = _tokens.NextRaw();
                if (source.IsChar("="))
                {
                    _tokens.SkipSpaces();
                    source = _tokens.NextRaw();
                }

                if (target.Kind != TokenKind.Command)
                {
                    throw new MathParseException(MathErrorCode.InvalidArgument, "\\let expects a command name.", token.Position);
                }

                _tokens.Macros[target.Text] = source.Kind == TokenKind.Command && _tokens.Macros.TryGetValue(source.Text, out var existing)
                    ? existing
                    : new Macro(0, null, [source]);
                return null;
            }
            case "newcommand":
                _tokens.NewCommand(token.Position, replace: false, provide: false);
                return null;
            case "renewcommand":
                _tokens.NewCommand(token.Position, replace: true, provide: false);
                return null;
            case "providecommand":
                _tokens.NewCommand(token.Position, replace: false, provide: true);
                return null;
            case "DeclareMathOperator":
            {
                var star = false;
                if (_tokens.Peek().IsChar("*"))
                {
                    _tokens.Next();
                    star = true;
                }

                var target = _tokens.ReadRawArgument();
                var body = _tokens.ReadRawArgument();
                if (target.Count != 1 || target[0].Kind != TokenKind.Command)
                {
                    throw new MathParseException(MathErrorCode.InvalidArgument, "\\DeclareMathOperator expects a command name.", token.Position);
                }

                var position = token.Position;
                List<Token> expansion =
                [
                    new(TokenKind.Command, star ? "operatornamewithlimits" : "operatorname", position),
                    new(TokenKind.BeginGroup, "{", position),
                    .. body,
                    new(TokenKind.EndGroup, "}", position)
                ];
                _tokens.Macros[target[0].Text] = new Macro(0, null, expansion);
                return null;
            }
            case "begin": return ParseEnvironment(token);
            case "end":
                throw new MathParseException(MathErrorCode.MismatchedEnvironment, "\\end without matching \\begin.", token.Position);
            case "substack": return ParseSubstack(token);
            case "char": return ParseCharCode(token);
            case "href":
                _tokens.ReadRawArgument();
                return ParseArgument();
            case "url": return new TextNode(ReadString(), MathVariant.Monospace);
            case "ce": return ParseTokens(Lexer.Tokenize(Chemistry.ToTeX(ReadString())));
            case "pu": return ParseTokens(Lexer.Tokenize(Chemistry.UnitsToTeX(ReadString())));
            case "uproot" or "leftroot":
                ReadString();
                return null;
            case "relax" or "nobreak" or "allowbreak" or "nolinebreak" or "protect" or "hline" or "hdashline" or "limits" or "nolimits":
                return null;
            case "displaystyle" or "textstyle" or "scriptstyle" or "scriptscriptstyle" or "color" or "rm" or "bf" or "it" or "sf" or "tt" or "cal" or "frak":
                throw new MathParseException(MathErrorCode.UnexpectedToken, $"\\{name} is not allowed here.", token.Position);
        }

        if (SizeScale(name) is not null)
        {
            throw new MathParseException(MathErrorCode.UnexpectedToken, $"\\{name} is not allowed here.", token.Position);
        }

        throw new MathParseException(MathErrorCode.UnknownCommand, $"Unknown command \\{name}.", token.Position);
    }

    private static AtomType ClassOf(Node node) => node switch
    {
        CharNode c => c.Type,
        ListNode { Items: [var only] } => ClassOf(only),
        ClassNode c => c.Type,
        OpNode => AtomType.Op,
        _ => AtomType.Ord
    };

    private FracNode ParseGenfrac()
    {
        var left = _tokens.ReadRawArgument();
        var right = _tokens.ReadRawArgument();
        var thicknessText = ReadString();
        var styleText = ReadString();
        double? thickness = thicknessText.Length == 0 ? null : ParseDimension(thicknessText, 0).Em;
        var style = styleText switch
        {
            "0" => FracStyle.Display,
            "1" => FracStyle.Text,
            _ => FracStyle.Auto
        };
        var num = ParseArgument();
        var den = ParseArgument();
        return new FracNode(num, den, thickness, style, DelimiterFromTokens(left), DelimiterFromTokens(right));
    }

    private int DelimiterFromTokens(List<Token> tokens)
    {
        if (tokens.Count == 0)
        {
            return 0;
        }

        _tokens.PushBack(tokens);
        return ReadDelimiter();
    }

    private LeftRightNode ParseLeftRight(Token token)
    {
        var left = ReadDelimiter();
        Enter(token.Position);
        var parts = new List<Node>();
        var middles = new List<int>();
        var savedVariant = _variant;
        while (true)
        {
            parts.Add(ParseExpression(static t => t.Kind == TokenKind.End || t.IsCommand("right") || t.IsCommand("middle")));
            var next = _tokens.Next();
            if (next.Kind == TokenKind.End)
            {
                throw new MathParseException(MathErrorCode.MissingDelimiter, "\\left without matching \\right.", token.Position);
            }

            if (next.IsCommand("middle"))
            {
                middles.Add(ReadDelimiter());
                continue;
            }

            var right = ReadDelimiter();
            _variant = savedVariant;
            _depth--;
            return new LeftRightNode(left, right, parts, middles);
        }
    }

    private DelimNode ParseBigDelimiter(string name)
    {
        var baseName = name.TrimEnd('l', 'r', 'm');
        var size = baseName switch
        {
            "big" => 1.2,
            "Big" => 1.8,
            "bigg" => 2.4,
            _ => 3.0
        };
        var type = name[^1] switch
        {
            'l' => AtomType.Open,
            'r' => AtomType.Close,
            'm' => AtomType.Rel,
            _ => AtomType.Ord
        };
        return new DelimNode(ReadDelimiter(), size, type);
    }

    private Node ParseExtensibleArrow(BraceKind kind)
    {
        var below = ParseOptionalArgument();
        var above = ParseArgument();
        return new ClassNode(AtomType.Rel, new StackNode(null, above, below, kind));
    }

    private Node ParseNot(Token token)
    {
        _tokens.SkipSpaces();
        var next = _tokens.Next();
        var key = next.Kind == TokenKind.Command ? "\\" + next.Text : next.Text;
        if (Symbols.Negations.TryGetValue(key, out var negated))
        {
            return Char(negated, AtomType.Rel);
        }

        if (next.Kind is TokenKind.End or TokenKind.EndGroup)
        {
            _tokens.PushBack(next);
            return Char(0x29F8, AtomType.Ord);
        }

        _tokens.PushBack(next);
        var target = ParseBase() ?? ListNode.Empty;
        var type = ClassOf(target) == AtomType.Ord ? AtomType.Rel : ClassOf(target);
        return new ClassNode(type, new OverlayNode(target, new CharNode(0x29F8, AtomType.Ord, MathVariant.Normal)));
    }

    private Node ParseCharCode(Token token)
    {
        _tokens.SkipSpaces();
        var radix = 10;
        var first = _tokens.Next();
        var digits = "";
        if (first.IsChar("\""))
        {
            radix = 16;
        }
        else if (first.IsChar("'"))
        {
            radix = 8;
        }
        else
        {
            _tokens.PushBack(first);
        }

        while (true)
        {
            var t = _tokens.Next();
            if (t.Kind == TokenKind.Char && t.Text is [var c] && Uri.IsHexDigit(c) && (radix == 16 || char.IsAsciiDigit(c)))
            {
                digits += c;
                continue;
            }

            _tokens.PushBack(t);
            break;
        }

        try
        {
            var value = Convert.ToInt32(digits, radix);
            if (value is < 0 or > 0x10FFFF || value is >= 0xD800 and <= 0xDFFF)
            {
                throw new FormatException();
            }

            return MakeChar(char.ConvertFromUtf32(value));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            throw new MathParseException(MathErrorCode.InvalidArgument, "Invalid character code.", token.Position);
        }
    }
}