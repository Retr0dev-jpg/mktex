using System.Net;
using System.Text;
using MkTeX;
using MkTeX.Rendering;

// Renders every sample to an HTML page for visual review: dotnet run --project tools/MkTeX.Gallery [output.html] [samples.txt]
if (args is ["--probe", .. var codes])
{
    Probe.Run([.. codes.Select(c => Convert.ToInt32(c, 16))]);
    return;
}

if (args is ["--bench", .. var benchArgs])
{
    var benchSamples = File.ReadAllLines(benchArgs.Length > 0 ? benchArgs[0] : Path.Combine(AppContext.BaseDirectory, "samples.txt"))
        .Where(l => l.Length > 0 && !l.StartsWith("//", StringComparison.Ordinal))
        .Select(l => l.StartsWith("D:", StringComparison.Ordinal) ? l[2..] : l)
        .ToList();
    foreach (var warm in benchSamples)
    {
        MathEngine.Layout(warm);
    }

    const int Rounds = 20;
    var watch = System.Diagnostics.Stopwatch.StartNew();
    for (var round = 0; round < Rounds; round++)
    {
        foreach (var sample in benchSamples)
        {
            MathEngine.Layout(sample, new MathOptions { Display = true });
        }
    }

    watch.Stop();
    var perFormula = watch.Elapsed.TotalMicroseconds / (Rounds * benchSamples.Count);
    Console.WriteLine($"{benchSamples.Count} formulas x {Rounds}: {watch.ElapsedMilliseconds} ms, {perFormula:F1} us/formula");
    return;
}

var output = args.Length > 0 ? args[0] : "gallery.html";
var samplesPath = args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "samples.txt");
var samples = File.ReadAllLines(samplesPath)
    .Where(l => l.Length > 0 && !l.StartsWith("//", StringComparison.Ordinal))
    .ToList();

var html = new StringBuilder();
html.Append("<!doctype html><meta charset=\"utf-8\"><title>MkTeX gallery</title><style>")
    .Append("body{font:14px 'Segoe UI',sans-serif;margin:24px;color:#111;background:#fff}")
    .Append("table{border-collapse:collapse;width:100%;table-layout:fixed}td{border-bottom:1px solid #ddd;padding:8px;vertical-align:middle;overflow-x:auto}")
    .Append("td.src{font:12px Consolas,monospace;color:#555;width:32%;white-space:pre-wrap;word-break:break-all}.err{color:#c00}")
    .Append("</style><h1>MkTeX gallery</h1><table>");
var index = 0;
var errors = 0;
foreach (var sample in samples)
{
    var display = sample.StartsWith("D:", StringComparison.Ordinal);
    var source = display ? sample[2..] : sample;
    var layout = MathEngine.Layout(source, new MathOptions { FontSize = 22, Display = display });
    var svg = SvgRenderer.Render(layout, new SvgOptions { IdPrefix = $"f{index++}-", Title = source });
    html.Append("<tr><td class=\"src\">").Append(WebUtility.HtmlEncode(source)).Append("</td><td>");
    if (layout.Error is { } error)
    {
        errors++;
        html.Append("<div class=\"err\">").Append(WebUtility.HtmlEncode($"{error.Code} @{error.Position}: {error.Message}")).Append("</div>");
    }

    html.Append("text ").Append(svg);
    if (layout.Tag is not null)
    {
        html.Append(" &nbsp; ").Append(SvgRenderer.Render(layout.Tag, new SvgOptions { IdPrefix = $"t{index}-" }));
    }

    html.Append(" text</td></tr>");
}

html.Append("</table>");
File.WriteAllText(output, html.ToString());
Console.WriteLine($"{samples.Count} samples, {errors} errors -> {Path.GetFullPath(output)}");
