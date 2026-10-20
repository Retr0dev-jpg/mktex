namespace MkTeX.Parsing;

internal sealed record Macro(int Parameters, IReadOnlyList<Token>? OptionalDefault, IReadOnlyList<Token> Body);

/// <summary>Token source with one-token lookahead and macro expansion.</summary>
internal sealed class TokenStream
{
    public const int MaxExpansions = 1000;
    public const int MaxTokens = 50_000;

    private const string BuiltinMacros = """
        \def\R{\mathbb{R}}\def\N{\mathbb{N}}\def\Z{\mathbb{Z}}\def\Q{\mathbb{Q}}\def\C{\mathbb{C}}
        \def\Reals{\mathbb{R}}\def\Complex{\mathbb{C}}\def\natnums{\mathbb{N}}\def\cnums{\mathbb{C}}
        \def\pmod#1{\mkern18mu(\mathrm{mod}\mkern6mu#1)}\def\mod#1{\mkern18mu\mathrm{mod}\mkern6mu#1}
        \def\pod#1{\mkern18mu(#1)}\def\bmod{\mathbin{\mathrm{mod}}}
        \def\bra#1{\mathinner{\langle{#1}|}}\def\ket#1{\mathinner{|{#1}\rangle}}
        \def\braket#1{\mathinner{\langle{#1}\rangle}}\def\Bra#1{\left\langle#1\right|}\def\Ket#1{\left|#1\right\rangle}
        \def\set#1{\{#1\}}\def\Set#1{\left\{#1\right\}}\def\abs#1{\left|#1\right|}\def\norm#1{\left\|#1\right\|}
        \def\mathstrut{\vphantom{(}}\def\strut{\vphantom{(}}\def\thinspace{\,}\def\medspace{\:}\def\thickspace{\;}
        \def\negthinspace{\!}\def\negmedspace{\mkern-4mu}\def\negthickspace{\mkern-5mu}\def\enspace{\kern0.5em}
        \def\enskip{\kern0.5em}\def\quad{\kern1em}\def\qquad{\kern2em}\def\nobreakspace{~}\def\space{\ }
        \def\iff{\;\Longleftrightarrow\;}\def\implies{\;\Longrightarrow\;}\def\impliedby{\;\Longleftarrow\;}
        \def\dfrac#1#2{\genfrac{}{}{}{0}{#1}{#2}}\def\tfrac#1#2{\genfrac{}{}{}{1}{#1}{#2}}
        \def\binom#1#2{\genfrac(){0pt}{}{#1}{#2}}\def\dbinom#1#2{\genfrac(){0pt}{0}{#1}{#2}}
        \def\tbinom#1#2{\genfrac(){0pt}{1}{#1}{#2}}\def\bm{\boldsymbol}\def\bold{\mathbf}\def\Bbb{\mathbb}
        \def\mathbfit{\boldsymbol}\def\neq{\ne}\def\land{\wedge}\def\lor{\vee}\def\vcentcolon{\mathrel{:}}
        \def\dblcolon{\mathrel{::}}\def\ratio{\mathrel{:}}\def\TeX{\textrm{T\kern-.1667em\raisebox{-.5ex}{E}\kern-.125em X}}
        \def\LaTeX{\textrm{L\kern-.36em\raisebox{.21em}{\scriptsize A}\kern-.15em T\kern-.1667em\raisebox{-.5ex}{E}\kern-.125em X}}
        \def\KaTeX{\textrm{K\kern-.17em\raisebox{.21em}{\scriptsize A}\kern-.15em T\kern-.1667em\raisebox{-.5ex}{E}\kern-.125em X}}
        \def\argmax{\operatorname*{arg\,max}}\def\argmin{\operatorname*{arg\,min}}
        \def\celsius{{}^\circ\mathrm{C}}\def\ohm{\Omega}\def\micro{\mu}\def\angstrom{\text{Å}}
        \def\eqref#1{\text{(#1)}}\def\ref#1{\text{#1}}\def\label#1{}\def\nonumber{\notag}
        \def\iddots{\mathinner{⋰}}\def\varlimsup{\overline{\lim}}\def\varliminf{\underline{\lim}}
        \def\varinjlim{\underrightarrow{\lim}}\def\varprojlim{\underleftarrow{\lim}}
        \def\llbracket{\mathopen{⟦}}\def\rrbracket{\mathclose{⟧}}\def\Darr{\Downarrow}\def\dArr{\Downarrow}
        \def\darr{\downarrow}\def\Uarr{\Uparrow}\def\uArr{\Uparrow}\def\uarr{\uparrow}\def\Larr{\Leftarrow}
        \def\lArr{\Leftarrow}\def\larr{\leftarrow}\def\Rarr{\Rightarrow}\def\rArr{\Rightarrow}\def\rarr{\rightarrow}
        \def\harr{\leftrightarrow}\def\Harr{\Leftrightarrow}\def\hArr{\Leftrightarrow}\def\lrarr{\leftrightarrow}
        \def\lrArr{\Leftrightarrow}\def\Lrarr{\Leftrightarrow}\def\isin{\in}\def\empty{\emptyset}
        \def\sub{\subset}\def\sube{\subseteq}\def\supe{\supseteq}\def\infin{\infty}\def\alef{\aleph}
        \def\alefsym{\aleph}\def\image{\Im}\def\real{\Re}\def\weierp{\wp}\def\exist{\exists}\def\plusmn{\pm}
        \def\Alpha{\mathrm{A}}\def\Beta{\mathrm{B}}\def\clubs{\clubsuit}\def\diamonds{\diamondsuit}
        \def\hearts{\heartsuit}\def\spades{\spadesuit}\def\sect{\S}\def\lang{\langle}\def\rang{\rangle}
        """;

    private readonly List<Token> _pending = [];
    private readonly IReadOnlyList<Token> _source;
    private int _index;
    private int _expansions;
    private int _produced;

    public TokenStream(string source, IReadOnlyDictionary<string, string>? userMacros)
    {
        _source = Lexer.Tokenize(source);
        Macros = new Dictionary<string, Macro>(StringComparer.Ordinal);
        LoadDefinitions(BuiltinMacros);
        if (userMacros is not null)
        {
            foreach (var (name, body) in userMacros)
            {
                var key = name.TrimStart('\\');
                var tokens = Lexer.Tokenize(body);
                var parameters = 0;
                for (var i = 0; i + 1 < tokens.Count; i++)
                {
                    if (tokens[i].Kind == TokenKind.Param && tokens[i + 1].Kind == TokenKind.Char &&
                        int.TryParse(tokens[i + 1].Text, out var n))
                    {
                        parameters = Math.Max(parameters, n);
                    }
                }

                Macros[key] = new Macro(parameters, null, tokens);
            }
        }
    }

    public Dictionary<string, Macro> Macros { get; }

    /// <summary>Returns the next unexpanded token.</summary>
    public Token NextRaw()
    {
        if (_pending.Count > 0)
        {
            var last = _pending[^1];
            _pending.RemoveAt(_pending.Count - 1);
            return last;
        }

        if (_index < _source.Count)
        {
            return _source[_index++];
        }

        return new Token(TokenKind.End, "", _source.Count > 0 ? _source[^1].Position + 1 : 0);
    }

    public Token PeekRaw()
    {
        var token = NextRaw();
        PushBack(token);
        return token;
    }

    public void PushBack(Token token) => _pending.Add(token);

    public void PushBack(IReadOnlyList<Token> tokens)
    {
        _produced += tokens.Count;
        if (_produced > MaxTokens)
        {
            throw new MathParseException(MathErrorCode.MacroLimit, "Macro expansion produced too many tokens.", tokens.Count > 0 ? tokens[0].Position : 0);
        }

        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            _pending.Add(tokens[i]);
        }
    }

    /// <summary>Returns the next token after expanding macros.</summary>
    public Token Next()
    {
        while (true)
        {
            var token = NextRaw();
            if (token.Kind != TokenKind.Command || !Macros.TryGetValue(token.Text, out var macro))
            {
                return token;
            }

            Expand(token, macro);
        }
    }

    public Token Peek()
    {
        var token = Next();
        PushBack(token);
        return token;
    }

    /// <summary>Peeks the next token that is not a space.</summary>
    public Token PeekNonSpace()
    {
        while (true)
        {
            var token = Next();
            if (token.Kind != TokenKind.Space)
            {
                PushBack(token);
                return token;
            }
        }
    }

    public void SkipSpaces()
    {
        while (true)
        {
            var token = NextRaw();
            if (token.Kind != TokenKind.Space)
            {
                PushBack(token);
                return;
            }
        }
    }

    /// <summary>Reads a balanced group or a single token without expansion.</summary>
    public List<Token> ReadRawArgument()
    {
        SkipSpaces();
        var first = NextRaw();
        if (first.Kind == TokenKind.End)
        {
            throw new MathParseException(MathErrorCode.MissingArgument, "Missing argument.", first.Position);
        }

        if (first.Kind != TokenKind.BeginGroup)
        {
            if (first.Kind == TokenKind.EndGroup)
            {
                throw new MathParseException(MathErrorCode.MissingArgument, "Missing argument.", first.Position);
            }

            return [first];
        }

        return ReadBalanced(first.Position, TokenKind.EndGroup, null);
    }

    /// <summary>Reads an optional [..] argument without expansion, or null when absent.</summary>
    public List<Token>? ReadRawOptional()
    {
        SkipSpaces();
        var first = NextRaw();
        if (!first.IsChar("["))
        {
            PushBack(first);
            return null;
        }

        return ReadBalanced(first.Position, null, "]");
    }

    private List<Token> ReadBalanced(int start, TokenKind? close, string? closeChar)
    {
        var result = new List<Token>();
        var depth = 0;
        while (true)
        {
            var token = NextRaw();
            switch (token.Kind)
            {
                case TokenKind.End:
                    throw new MathParseException(MathErrorCode.UnbalancedGroup, "Missing closing brace.", start);
                case TokenKind.BeginGroup:
                    depth++;
                    break;
                case TokenKind.EndGroup when depth == 0 && close == TokenKind.EndGroup:
                    return result;
                case TokenKind.EndGroup when depth == 0:
                    throw new MathParseException(MathErrorCode.UnbalancedGroup, "Unexpected closing brace.", token.Position);
                case TokenKind.EndGroup:
                    depth--;
                    break;
                case TokenKind.Char when depth == 0 && token.Text == closeChar:
                    return result;
            }

            result.Add(token);
        }
    }

    private void Expand(Token token, Macro macro)
    {
        if (++_expansions > MaxExpansions)
        {
            throw new MathParseException(MathErrorCode.MacroLimit, "Too many macro expansions.", token.Position);
        }

        var args = new List<List<Token>>();
        var first = 0;
        if (macro.OptionalDefault is not null)
        {
            args.Add(ReadRawOptional() ?? [.. macro.OptionalDefault]);
            first = 1;
        }

        for (var i = first; i < macro.Parameters; i++)
        {
            args.Add(ReadRawArgument());
        }

        var body = new List<Token>(macro.Body.Count);
        for (var i = 0; i < macro.Body.Count; i++)
        {
            var t = macro.Body[i];
            if (t.Kind == TokenKind.Param && i + 1 < macro.Body.Count)
            {
                var next = macro.Body[i + 1];
                if (next.Kind == TokenKind.Char && int.TryParse(next.Text, out var n) && n >= 1 && n <= args.Count)
                {
                    body.AddRange(args[n - 1].Select(a => a with { Position = token.Position }));
                    i++;
                    continue;
                }

                if (next.Kind == TokenKind.Param)
                {
                    body.Add(next);
                    i++;
                    continue;
                }
            }

            body.Add(t with { Position = token.Position });
        }

        PushBack(body);
    }

    /// <summary>Handles \def\name#1#2{body}.</summary>
    public void Define(int position)
    {
        var nameToken = NextRaw();
        if (nameToken.Kind != TokenKind.Command)
        {
            throw new MathParseException(MathErrorCode.InvalidArgument, "\\def expects a command name.", position);
        }

        var parameters = 0;
        while (true)
        {
            var t = NextRaw();
            if (t.Kind == TokenKind.Param)
            {
                var n = NextRaw();
                if (n.Kind != TokenKind.Char || n.Text != (parameters + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))
                {
                    throw new MathParseException(MathErrorCode.InvalidArgument, "Invalid parameter in \\def.", n.Position);
                }

                parameters++;
                continue;
            }

            if (t.Kind != TokenKind.BeginGroup)
            {
                throw new MathParseException(MathErrorCode.InvalidArgument, "\\def expects a body.", t.Position);
            }

            break;
        }

        var body = ReadBalanced(position, TokenKind.EndGroup, null);
        Macros[nameToken.Text] = new Macro(parameters, null, body);
    }

    /// <summary>Handles \newcommand{\name}[n][default]{body} and its variants.</summary>
    public void NewCommand(int position, bool replace, bool provide)
    {
        var nameTokens = ReadRawArgument();
        if (nameTokens.Count != 1 || nameTokens[0].Kind != TokenKind.Command)
        {
            throw new MathParseException(MathErrorCode.InvalidArgument, "\\newcommand expects a command name.", position);
        }

        var name = nameTokens[0].Text;
        var parameters = 0;
        var count = ReadRawOptional();
        if (count is not null)
        {
            var text = string.Concat(count.Select(t => t.Text)).Trim();
            if (!int.TryParse(text, out parameters) || parameters is < 0 or > 9)
            {
                throw new MathParseException(MathErrorCode.InvalidArgument, "Invalid number of parameters.", position);
            }
        }

        var optional = parameters > 0 ? ReadRawOptional() : null;
        var body = ReadRawArgument();
        var exists = Macros.ContainsKey(name) || Symbols.TryGet(name, out _);
        if (provide && exists)
        {
            return;
        }

        if (!replace && !provide && exists && !Macros.ContainsKey(name))
        {
            throw new MathParseException(MathErrorCode.InvalidArgument, $"\\{name} is already defined.", position);
        }

        Macros[name] = new Macro(parameters, optional, body);
    }

    private void LoadDefinitions(string source)
    {
        var tokens = Lexer.Tokenize(source);
        var saved = new List<Token>(_pending);
        _pending.Clear();
        PushBack(tokens);
        while (true)
        {
            var token = NextRaw();
            if (token.Kind == TokenKind.End)
            {
                break;
            }

            if (token.IsCommand("def"))
            {
                Define(token.Position);
            }

            if (_pending.Count == 0)
            {
                break;
            }
        }

        _produced = 0;
        _pending.AddRange(saved);
    }
}
