using System.Globalization;

namespace MkTeX.Parsing;

/// <summary>Resolves LaTeX/xcolor and CSS color names to #RRGGBB.</summary>
internal static class Colors
{
    private static readonly Dictionary<string, string> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "#000000", ["white"] = "#FFFFFF", ["red"] = "#FF0000", ["green"] = "#008000", ["blue"] = "#0000FF",
        ["cyan"] = "#00FFFF", ["magenta"] = "#FF00FF", ["yellow"] = "#FFFF00", ["gray"] = "#808080", ["grey"] = "#808080",
        ["darkgray"] = "#404040", ["lightgray"] = "#BFBFBF", ["orange"] = "#FF8000", ["purple"] = "#BF0040",
        ["brown"] = "#BF8040", ["lime"] = "#BFFF00", ["olive"] = "#808000", ["pink"] = "#FFBFBF", ["teal"] = "#008080",
        ["violet"] = "#800080", ["navy"] = "#000080", ["maroon"] = "#800000", ["silver"] = "#C0C0C0", ["gold"] = "#FFD700",
        ["indigo"] = "#4B0082", ["aqua"] = "#00FFFF", ["fuchsia"] = "#FF00FF", ["coral"] = "#FF7F50", ["crimson"] = "#DC143C",
        ["salmon"] = "#FA8072", ["tomato"] = "#FF6347", ["turquoise"] = "#40E0D0", ["khaki"] = "#F0E68C",
        ["orchid"] = "#DA70D6", ["plum"] = "#DDA0DD", ["tan"] = "#D2B48C", ["beige"] = "#F5F5DC", ["skyblue"] = "#87CEEB",
        ["steelblue"] = "#4682B4", ["darkred"] = "#8B0000", ["darkgreen"] = "#006400", ["darkblue"] = "#00008B",
        ["darkorange"] = "#FF8C00", ["forestgreen"] = "#009B55", ["royalblue"] = "#0071BC", ["navyblue"] = "#006EB8",
        ["midnightblue"] = "#006795", ["cornflowerblue"] = "#41B0E4", ["emerald"] = "#00A99D", ["junglegreen"] = "#00A99A",
        ["seagreen"] = "#3FBC9D", ["limegreen"] = "#8DC73E", ["yellowgreen"] = "#98CC70", ["springgreen"] = "#C6DC67",
        ["goldenrod"] = "#FFDF42", ["dandelion"] = "#FDBC42", ["apricot"] = "#FBB982", ["peach"] = "#F7965A",
        ["melon"] = "#F89E7B", ["yelloworange"] = "#FAA21A", ["burntorange"] = "#F7921D", ["bittersweet"] = "#C04F17",
        ["redorange"] = "#F26035", ["mahogany"] = "#A9341F", ["brickred"] = "#B6321C", ["orangered"] = "#ED135A",
        ["rubinered"] = "#ED017D", ["wildstrawberry"] = "#EE2967", ["carnationpink"] = "#F282B4", ["rhodamine"] = "#EF559F",
        ["rubine"] = "#ED017D", ["mulberry"] = "#A93C93", ["redviolet"] = "#A1246B", ["lavender"] = "#F49EC4",
        ["thistle"] = "#D883B7", ["darkorchid"] = "#A4538A", ["blueviolet"] = "#473992", ["periwinkle"] = "#7977B8",
        ["cadetblue"] = "#74729A", ["processblue"] = "#00B0F0", ["ceruleanblue"] = "#00A2E3", ["cerulean"] = "#00A2E3",
        ["bluegreen"] = "#00B3B8", ["aquamarine"] = "#00B5BE", ["pinegreen"] = "#008B72", ["olivegreen"] = "#3C8031",
        ["rawsienna"] = "#974006", ["sepia"] = "#671800", ["rawumber"] = "#A0522D", ["sienna"] = "#A0522D"
    };

    public static string Parse(string text, int position)
    {
        var value = text.Trim();
        if (Named.TryGetValue(value, out var named))
        {
            return named;
        }

        var hex = value.StartsWith('#') ? value[1..] : value;
        if (hex.Length == 3 && hex.All(Uri.IsHexDigit))
        {
            hex = string.Concat(hex.Select(c => $"{c}{c}"));
        }

        if (hex.Length == 6 && hex.All(Uri.IsHexDigit) && (value.StartsWith('#') || hex.Any(char.IsAsciiDigit)))
        {
            return "#" + hex.ToUpperInvariant();
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 3 && parts.All(p => double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
        {
            var numbers = parts.Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();
            var scale = numbers.All(n => n <= 1) ? 255 : 1;
            return "#" + string.Concat(numbers.Select(n => ((int)Math.Clamp(Math.Round(n * scale), 0, 255)).ToString("X2", CultureInfo.InvariantCulture)));
        }

        throw new MathParseException(MathErrorCode.InvalidArgument, $"Unknown color '{text}'.", position);
    }
}
