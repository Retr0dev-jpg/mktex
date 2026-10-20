using System.Text;

namespace MkTeX.Parsing;

internal enum TokenKind
{
    Command,
    Char,
    Space,
    BeginGroup,
    EndGroup,
    Superscript,
    Subscript,
    Align,
    Param,
    MathShift,
    Active,
    End
}

internal readonly record struct Token(TokenKind Kind, string Text, int Position)
{
    public bool Is(TokenKind kind, string text) => Kind == kind && Text == text;

    public bool IsChar(string text) => Kind == TokenKind.Char && Text == text;

    public bool IsCommand(string name) => Kind == TokenKind.Command && Text == name;

    public override string ToString() => Kind switch
    {
        TokenKind.Command => "\\" + Text,
        TokenKind.End => "",
        _ => Text
    };
}

internal static class Lexer
{
    public static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            var start = i;
            switch (c)
            {
                case '\\':
                    i++;
                    if (i >= source.Length)
                    {
                        throw new MathParseException(MathErrorCode.UnexpectedEnd, "Trailing backslash.", start);
                    }

                    if (char.IsAsciiLetter(source[i]))
                    {
                        var nameStart = i;
                        while (i < source.Length && char.IsAsciiLetter(source[i]))
                        {
                            i++;
                        }

                        tokens.Add(new Token(TokenKind.Command, source[nameStart..i], start));
                        while (i < source.Length && IsSpace(source[i]))
                        {
                            i++;
                        }
                    }
                    else
                    {
                        var length = char.IsHighSurrogate(source[i]) && i + 1 < source.Length ? 2 : 1;
                        tokens.Add(new Token(TokenKind.Command, source.Substring(i, length), start));
                        i += length;
                    }

                    continue;
                case '%':
                    while (i < source.Length && source[i] != '\n')
                    {
                        i++;
                    }

                    continue;
                case '{':
                    tokens.Add(new Token(TokenKind.BeginGroup, "{", start));
                    break;
                case '}':
                    tokens.Add(new Token(TokenKind.EndGroup, "}", start));
                    break;
                case '^':
                    tokens.Add(new Token(TokenKind.Superscript, "^", start));
                    break;
                case '_':
                    tokens.Add(new Token(TokenKind.Subscript, "_", start));
                    break;
                case '&':
                    tokens.Add(new Token(TokenKind.Align, "&", start));
                    break;
                case '#':
                    tokens.Add(new Token(TokenKind.Param, "#", start));
                    break;
                case '$':
                    tokens.Add(new Token(TokenKind.MathShift, "$", start));
                    break;
                case '~':
                    tokens.Add(new Token(TokenKind.Active, "~", start));
                    break;
                default:
                    if (IsSpace(c))
                    {
                        while (i < source.Length && IsSpace(source[i]))
                        {
                            i++;
                        }

                        tokens.Add(new Token(TokenKind.Space, " ", start));
                        continue;
                    }

                    if (Rune.TryGetRuneAt(source, i, out var rune))
                    {
                        tokens.Add(new Token(TokenKind.Char, rune.ToString(), start));
                        i += rune.Utf16SequenceLength;
                    }
                    else
                    {
                        tokens.Add(new Token(TokenKind.Char, "\uFFFD", start));
                        i++;
                    }

                    continue;
            }

            i++;
        }

        return tokens;
    }

    private static bool IsSpace(char c) => c is ' ' or '\t' or '\n' or '\r';
}
