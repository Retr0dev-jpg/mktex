using System.Text;

namespace MkTeX.Parsing;

/// <summary>Translates a practical subset of mhchem (\ce, \pu) into math-mode TeX.</summary>
internal static class Chemistry
{
    private static readonly (string Text, string Command)[] Arrows =
    [
        ("<=>>", "\\xrightleftharpoons"), ("<<=>", "\\xrightleftharpoons"), ("<=>", "\\xrightleftharpoons"),
        ("<->", "\\xleftrightarrow"), ("<-->", "\\xleftrightarrow"), ("->", "\\xrightarrow"), ("<-", "\\xleftarrow")
    ];

    public static string ToTeX(string source)
    {
        var output = new StringBuilder();
        var i = 0;
        var afterSpecies = false;
        var atStart = true;
        while (i < source.Length)
        {
            var c = source[i];
            var arrow = Arrows.FirstOrDefault(a => string.CompareOrdinal(source, i, a.Text, 0, a.Text.Length) == 0);
            if (arrow.Text is not null)
            {
                i += arrow.Text.Length;
                var above = ReadBracket(source, ref i);
                var below = ReadBracket(source, ref i);
                output.Append(arrow.Command);
                if (below is not null)
                {
                    output.Append('[').Append(ToTeX(below)).Append(']');
                }

                output.Append('{').Append(above is null ? "" : ToTeX(above)).Append('}');
                afterSpecies = false;
                atStart = true;
                continue;
            }

            if (c == ' ')
            {
                i++;
                if (i + 1 < source.Length && source[i] is '^' or 'v' && source[i + 1] == ' ')
                {
                    output.Append(source[i] == '^' ? "\\uparrow " : "\\downarrow ");
                    i += 2;
                    continue;
                }

                if (i + 1 < source.Length && source[i] == '+' && source[i + 1] == ' ')
                {
                    output.Append("{}+{}");
                    i += 2;
                    atStart = true;
                    afterSpecies = false;
                    continue;
                }

                output.Append("\\ ");
                atStart = true;
                afterSpecies = false;
                continue;
            }

            if (c == '\\')
            {
                var start = i++;
                while (i < source.Length && char.IsAsciiLetter(source[i]))
                {
                    i++;
                }

                if (i == start + 1 && i < source.Length)
                {
                    i++;
                }

                output.Append(source, start, i - start).Append(' ');
                continue;
            }

            if (c == '$')
            {
                var end = source.IndexOf('$', i + 1);
                end = end < 0 ? source.Length : end;
                output.Append('{').Append(source, i + 1, end - i - 1).Append('}');
                i = Math.Min(source.Length, end + 1);
                continue;
            }

            if (c == '{')
            {
                var end = MatchBrace(source, i);
                output.Append("{\\mathrm{").Append(source, i + 1, end - i - 1).Append("}}");
                i = Math.Min(source.Length, end + 1);
                afterSpecies = true;
                continue;
            }

            if (char.IsAsciiDigit(c) || (c == '.' && afterSpecies is false && atStart))
            {
                var start = i;
                while (i < source.Length && (char.IsAsciiDigit(source[i]) || source[i] is '.' or '/'))
                {
                    i++;
                }

                var number = source[start..i];
                if (afterSpecies)
                {
                    output.Append("_{").Append(number).Append('}');
                }
                else
                {
                    output.Append(number).Append("\\,");
                }

                continue;
            }

            if (c == '^')
            {
                i++;
                string charge;
                if (i < source.Length && source[i] == '{')
                {
                    var end = MatchBrace(source, i);
                    charge = source[(i + 1)..end];
                    i = Math.Min(source.Length, end + 1);
                }
                else
                {
                    var start = i;
                    while (i < source.Length && (char.IsAsciiLetterOrDigit(source[i]) || source[i] is '+' or '-'))
                    {
                        i++;
                    }

                    charge = source[start..i];
                }

                output.Append("^{").Append(Charge(charge)).Append('}');
                continue;
            }

            if (c is '+' or '-' && afterSpecies && (i + 1 == source.Length || source[i + 1] is ' ' or '+' or '-' or ')'))
            {
                var start = i;
                while (i < source.Length && source[i] is '+' or '-')
                {
                    i++;
                }

                output.Append("^{").Append(Charge(source[start..i])).Append('}');
                continue;
            }

            if (char.IsAsciiLetter(c))
            {
                var start = i++;
                if (char.IsAsciiLetterUpper(c))
                {
                    while (i < source.Length && char.IsAsciiLetterLower(source[i]))
                    {
                        i++;
                    }
                }
                else
                {
                    while (i < source.Length && char.IsAsciiLetter(source[i]))
                    {
                        i++;
                    }
                }

                output.Append("\\mathrm{").Append(source, start, i - start).Append('}');
                afterSpecies = true;
                atStart = false;
                continue;
            }

            switch (c)
            {
                case '*' or '·':
                    output.Append("\\cdot ");
                    afterSpecies = false;
                    atStart = true;
                    break;
                case '=':
                    output.Append("{=}");
                    afterSpecies = false;
                    break;
                case '#':
                    output.Append("{\\equiv}");
                    afterSpecies = false;
                    break;
                case '(' or '[':
                    output.Append(c);
                    afterSpecies = false;
                    atStart = true;
                    break;
                case ')' or ']':
                    output.Append(c);
                    afterSpecies = true;
                    break;
                case '+':
                    output.Append('+');
                    afterSpecies = false;
                    atStart = true;
                    break;
                case '-':
                    output.Append("{-}");
                    afterSpecies = false;
                    break;
                case '%' or '&' or '#' or '_':
                    output.Append('\\').Append(c);
                    break;
                default:
                    output.Append(c);
                    break;
            }

            i++;
        }

        return output.ToString();
    }

    public static string UnitsToTeX(string source)
    {
        var output = new StringBuilder();
        foreach (var part in source.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (output.Length > 0)
            {
                output.Append("\\,");
            }

            if (double.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                output.Append(part.Replace("e", "\\times10^{", StringComparison.OrdinalIgnoreCase)).Append(part.Contains('e', StringComparison.OrdinalIgnoreCase) ? "}" : "");
                continue;
            }

            output.Append("\\mathrm{").Append(part.Replace("^", "}^{", StringComparison.Ordinal).Replace("*", "}\\cdot\\mathrm{", StringComparison.Ordinal)).Append('}');
        }

        return output.ToString();
    }

    private static string Charge(string charge) => charge;

    private static string? ReadBracket(string source, ref int i)
    {
        if (i >= source.Length || source[i] != '[')
        {
            return null;
        }

        var depth = 0;
        for (var j = i; j < source.Length; j++)
        {
            if (source[j] == '[')
            {
                depth++;
            }
            else if (source[j] == ']' && --depth == 0)
            {
                var content = source[(i + 1)..j];
                i = j + 1;
                return content;
            }
        }

        return null;
    }

    private static int MatchBrace(string source, int open)
    {
        var depth = 0;
        for (var j = open; j < source.Length; j++)
        {
            if (source[j] == '{')
            {
                depth++;
            }
            else if (source[j] == '}' && --depth == 0)
            {
                return j;
            }
        }

        return source.Length;
    }
}
