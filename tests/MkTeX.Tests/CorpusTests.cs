using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using MkTeX.Parsing;
using MkTeX.Rendering;

namespace MkTeX.Tests;

/// <summary>
/// Renders every corpus formula and compares SHA-256 hashes of the SVG with Corpus/snapshots.tsv.
/// Set MKTEX_UPDATE_SNAPSHOTS=1 to rewrite the file after reviewing the gallery.
/// </summary>
public class CorpusTests
{
    private static string SourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    public static IEnumerable<string> Formulas()
    {
        var curated = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Corpus", "formulas.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith("//", StringComparison.Ordinal));
        var symbols = Symbols.Names.Order(StringComparer.Ordinal).Select(n => $"a \\{n} b");
        var operators = Symbols.Operators.Keys.Order(StringComparer.Ordinal).Select(n => $"D:\\{n}_{{x}}^{{y}} f");
        var accents = Symbols.Accents.Keys.Order(StringComparer.Ordinal).Select(n => $"\\{n}{{x}} \\{n}{{AB}}");
        return curated.Concat(symbols).Concat(operators).Concat(accents).Distinct();
    }

    [Fact]
    public void CorpusIsLargeEnough()
    {
        Assert.True(Formulas().Count() >= 500);
    }

    [Fact]
    public void CuratedFormulasParse()
    {
        var failures = new List<string>();
        foreach (var formula in Formulas())
        {
            var source = formula.StartsWith("D:", StringComparison.Ordinal) ? formula[2..] : formula;
            if (MathEngine.Validate(source) is { } error)
            {
                failures.Add($"{source} -> {error.Code}: {error.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures));
    }

    [Fact]
    public void SnapshotsMatch()
    {
        var actual = new StringBuilder();
        foreach (var formula in Formulas())
        {
            var display = formula.StartsWith("D:", StringComparison.Ordinal);
            var source = display ? formula[2..] : formula;
            var svg = MathEngine.ToSvg(source, new MathOptions { FontSize = 20, Display = display }, new SvgOptions { Title = source });
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(svg)))[..16];
            actual.Append(hash).Append('\t').Append(formula).Append('\n');
        }

        var path = Path.Combine(SourceDirectory(), "Corpus", "snapshots.tsv");
        var expected = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : "";
        if (Environment.GetEnvironmentVariable("MKTEX_UPDATE_SNAPSHOTS") == "1" || expected.Length == 0)
        {
            File.WriteAllText(path, actual.ToString());
            return;
        }

        var expectedLines = expected.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var changed = actual.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(l => !expectedLines.Contains(l)).ToList();
        Assert.True(changed.Count == 0, $"{changed.Count} snapshots changed:\n" + string.Join('\n', changed.Take(20)));
    }
}
