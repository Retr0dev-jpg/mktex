using System.Collections.Concurrent;

namespace MkTeX.Fonts;

/// <summary>
/// An OpenType font with a MATH table and TrueType outlines. All metrics are returned in em units.
/// </summary>
public sealed class MathFont
{
    private const int MaxCompositeDepth = 8;

    private static readonly Lazy<MathFont> DefaultFont = new(LoadDefault, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly FontReader reader;
    private readonly Dictionary<string, (int Offset, int Length)> tables = new(StringComparer.Ordinal);
    private readonly Dictionary<int, ushort> cmap = [];
    private readonly ushort[] advances;
    private readonly int[] glyphOffsets;
    private readonly int glyfOffset;
    private readonly double[] constants = new double[Enum.GetValues<MathConstant>().Length];
    private readonly Dictionary<ushort, double> italics = [];
    private readonly Dictionary<ushort, double> topAccents = [];
    private readonly HashSet<ushort> extendedShapes = [];
    private readonly Dictionary<ushort, GlyphConstruction> vertical = [];
    private readonly Dictionary<ushort, GlyphConstruction> horizontal = [];
    private readonly ConcurrentDictionary<ushort, GlyphOutline> outlines = new();
    private readonly ConcurrentDictionary<ushort, GlyphBounds> bounds = new();

    private MathFont(byte[] data)
    {
        reader = new FontReader(data);
        ReadDirectory();
        UnitsPerEm = reader.U16(Table("head").Offset + 18);
        var longOffsets = reader.I16(Table("head").Offset + 50) != 0;
        GlyphCount = reader.U16(Table("maxp").Offset + 4);
        var hhea = Table("hhea").Offset;
        Ascender = reader.I16(hhea + 4) / (double)UnitsPerEm;
        Descender = -reader.I16(hhea + 6) / (double)UnitsPerEm;
        advances = ReadAdvances(reader.U16(hhea + 34));
        glyphOffsets = ReadLoca(longOffsets);
        glyfOffset = Table("glyf").Offset;
        ReadCmap();
        ReadMath();
    }

    public static MathFont Default => DefaultFont.Value;

    public int UnitsPerEm { get; }

    public int GlyphCount { get; }

    public double Ascender { get; }

    public double Descender { get; }

    public double MinConnectorOverlap { get; private set; }

    /// <summary>Loads an OpenType MATH font with TrueType (glyf) outlines.</summary>
    public static MathFont Load(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        try
        {
            return new MathFont(data);
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException("Not a supported OpenType MATH font.", ex);
        }
    }

    public ushort GlyphIndex(int codePoint) => cmap.GetValueOrDefault(codePoint);

    public bool HasGlyph(int codePoint) => cmap.ContainsKey(codePoint);

    public double Advance(ushort glyph) => advances[Math.Min(glyph, advances.Length - 1)] / (double)UnitsPerEm;

    public double Constant(MathConstant constant) => constants[(int)constant];

    public double Italic(ushort glyph) => italics.GetValueOrDefault(glyph);

    public double? TopAccent(ushort glyph) => topAccents.TryGetValue(glyph, out var value) ? value : null;

    public bool IsExtendedShape(ushort glyph) => extendedShapes.Contains(glyph);

    public GlyphConstruction? Vertical(ushort glyph) => vertical.GetValueOrDefault(glyph);

    public GlyphConstruction? Horizontal(ushort glyph) => horizontal.GetValueOrDefault(glyph);

    public GlyphBounds Bounds(ushort glyph) => bounds.GetOrAdd(glyph, ReadBounds);

    public GlyphOutline Outline(ushort glyph) => outlines.GetOrAdd(glyph, g =>
    {
        var commands = new List<PathCommand>();
        AppendOutline(g, commands, 1, 0, 0, 1, 0, 0, 0);
        return new GlyphOutline(commands);
    });

    private static MathFont LoadDefault()
    {
        using var stream = typeof(MathFont).Assembly.GetManifestResourceStream("MkTeX.STIXTwoMath-Regular.ttf")
            ?? throw new InvalidOperationException("The bundled math font is missing.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return new MathFont(memory.ToArray());
    }

    private (int Offset, int Length) Table(string tag) => tables.TryGetValue(tag, out var table)
        ? table
        : throw new InvalidDataException($"Missing '{tag}' table.");

    private void ReadDirectory()
    {
        var count = reader.U16(4);
        for (var i = 0; i < count; i++)
        {
            var record = 12 + i * 16;
            tables[reader.Tag(record)] = ((int)reader.U32(record + 8), (int)reader.U32(record + 12));
        }
    }

    private ushort[] ReadAdvances(int metrics)
    {
        var hmtx = Table("hmtx").Offset;
        var result = new ushort[Math.Max(1, GlyphCount)];
        ushort last = 0;
        for (var i = 0; i < result.Length; i++)
        {
            if (i < metrics)
            {
                last = reader.U16(hmtx + i * 4);
            }

            result[i] = last;
        }

        return result;
    }

    private int[] ReadLoca(bool longOffsets)
    {
        var loca = Table("loca").Offset;
        var result = new int[GlyphCount + 1];
        for (var i = 0; i <= GlyphCount; i++)
        {
            result[i] = longOffsets ? (int)reader.U32(loca + i * 4) : reader.U16(loca + i * 2) * 2;
        }

        return result;
    }

    private void ReadCmap()
    {
        var cmapOffset = Table("cmap").Offset;
        var count = reader.U16(cmapOffset + 2);
        int best = -1, bestScore = -1;
        for (var i = 0; i < count; i++)
        {
            var record = cmapOffset + 4 + i * 8;
            var platform = reader.U16(record);
            var encoding = reader.U16(record + 2);
            var offset = cmapOffset + (int)reader.U32(record + 4);
            var format = reader.U16(offset);
            var score = (platform, encoding, format) switch
            {
                (3, 10, 12) => 4,
                (0, _, 12) => 3,
                (3, 1, 4) => 2,
                (0, _, 4) => 1,
                _ => -1
            };
            if (score > bestScore)
            {
                bestScore = score;
                best = offset;
            }
        }

        if (best < 0)
        {
            throw new InvalidDataException("No Unicode cmap.");
        }

        if (reader.U16(best) == 12)
        {
            var groups = reader.U32(best + 12);
            for (var i = 0; i < groups; i++)
            {
                var group = best + 16 + i * 12;
                var start = reader.U32(group);
                var end = Math.Min(reader.U32(group + 4), start + 0x10000);
                var glyph = reader.U32(group + 8);
                for (var cp = start; cp <= end; cp++)
                {
                    cmap[(int)cp] = (ushort)(glyph + cp - start);
                }
            }

            return;
        }

        var segments = reader.U16(best + 6) / 2;
        var ends = best + 14;
        var starts = ends + segments * 2 + 2;
        var deltas = starts + segments * 2;
        var ranges = deltas + segments * 2;
        for (var s = 0; s < segments; s++)
        {
            int end = reader.U16(ends + s * 2), start = reader.U16(starts + s * 2);
            var delta = reader.I16(deltas + s * 2);
            var rangeOffset = reader.U16(ranges + s * 2);
            for (var cp = start; cp <= end && cp != 0xFFFF; cp++)
            {
                int glyph;
                if (rangeOffset == 0)
                {
                    glyph = (cp + delta) & 0xFFFF;
                }
                else
                {
                    var address = ranges + s * 2 + rangeOffset + (cp - start) * 2;
                    glyph = reader.U16(address);
                    if (glyph != 0)
                    {
                        glyph = (glyph + delta) & 0xFFFF;
                    }
                }

                if (glyph != 0)
                {
                    cmap[cp] = (ushort)glyph;
                }
            }
        }
    }

    private void ReadMath()
    {
        var math = Table("MATH").Offset;
        var constantsOffset = math + reader.U16(math + 4);
        var glyphInfo = math + reader.U16(math + 6);
        var variants = math + reader.U16(math + 8);
        double em = UnitsPerEm;

        constants[0] = reader.I16(constantsOffset);
        constants[1] = reader.I16(constantsOffset + 2);
        constants[2] = reader.U16(constantsOffset + 4) / em;
        constants[3] = reader.U16(constantsOffset + 6) / em;
        for (var i = 0; i < 51; i++)
        {
            constants[4 + i] = reader.I16(constantsOffset + 8 + i * 4) / em;
        }

        constants[55] = reader.I16(constantsOffset + 8 + 51 * 4);

        ReadValueTable(glyphInfo, reader.U16(glyphInfo), italics);
        ReadValueTable(glyphInfo, reader.U16(glyphInfo + 2), topAccents);
        var extended = reader.U16(glyphInfo + 4);
        if (extended != 0)
        {
            foreach (var glyph in Coverage(glyphInfo + extended))
            {
                extendedShapes.Add(glyph);
            }
        }

        MinConnectorOverlap = reader.U16(variants) / em;
        var verticalCoverage = reader.U16(variants + 2);
        var horizontalCoverage = reader.U16(variants + 4);
        var verticalCount = reader.U16(variants + 6);
        var horizontalCount = reader.U16(variants + 8);
        ReadConstructions(variants, verticalCoverage, verticalCount, 10, vertical);
        ReadConstructions(variants, horizontalCoverage, horizontalCount, 10 + verticalCount * 2, horizontal);
    }

    private void ReadValueTable(int parent, int relative, Dictionary<ushort, double> target)
    {
        if (relative == 0)
        {
            return;
        }

        var table = parent + relative;
        var glyphs = Coverage(table + reader.U16(table));
        var count = reader.U16(table + 2);
        for (var i = 0; i < count && i < glyphs.Count; i++)
        {
            target[glyphs[i]] = reader.I16(table + 4 + i * 4) / (double)UnitsPerEm;
        }
    }

    private void ReadConstructions(int variants, int coverage, int count, int arrayOffset, Dictionary<ushort, GlyphConstruction> target)
    {
        if (coverage == 0 || count == 0)
        {
            return;
        }

        var glyphs = Coverage(variants + coverage);
        double em = UnitsPerEm;
        for (var i = 0; i < count && i < glyphs.Count; i++)
        {
            var construction = variants + reader.U16(variants + arrayOffset + i * 2);
            var assemblyOffset = reader.U16(construction);
            var variantCount = reader.U16(construction + 2);
            var list = new List<GlyphVariant>(variantCount);
            for (var v = 0; v < variantCount; v++)
            {
                var record = construction + 4 + v * 4;
                list.Add(new GlyphVariant(reader.U16(record), reader.U16(record + 2) / em));
            }

            var parts = new List<GlyphPart>();
            if (assemblyOffset != 0)
            {
                var assembly = construction + assemblyOffset;
                var partCount = reader.U16(assembly + 4);
                for (var p = 0; p < partCount; p++)
                {
                    var part = assembly + 6 + p * 10;
                    parts.Add(new GlyphPart(
                        reader.U16(part),
                        reader.U16(part + 2) / em,
                        reader.U16(part + 4) / em,
                        reader.U16(part + 6) / em,
                        (reader.U16(part + 8) & 1) != 0));
                }
            }

            target[glyphs[i]] = new GlyphConstruction(list, parts);
        }
    }

    private List<ushort> Coverage(int offset)
    {
        var result = new List<ushort>();
        var format = reader.U16(offset);
        var count = reader.U16(offset + 2);
        if (format == 1)
        {
            for (var i = 0; i < count; i++)
            {
                result.Add(reader.U16(offset + 4 + i * 2));
            }
        }
        else if (format == 2)
        {
            for (var i = 0; i < count; i++)
            {
                var range = offset + 4 + i * 6;
                int start = reader.U16(range), end = reader.U16(range + 2), index = reader.U16(range + 4);
                while (result.Count < index)
                {
                    result.Add(0);
                }

                for (var g = start; g <= end; g++)
                {
                    result.Add((ushort)g);
                }
            }
        }

        return result;
    }

    private (int Offset, int Length) GlyphData(ushort glyph)
    {
        if (glyph >= GlyphCount)
        {
            return (0, 0);
        }

        var start = glyphOffsets[glyph];
        return (glyfOffset + start, glyphOffsets[glyph + 1] - start);
    }

    private GlyphBounds ReadBounds(ushort glyph)
    {
        var (offset, length) = GlyphData(glyph);
        if (length < 10)
        {
            return default;
        }

        double em = UnitsPerEm;
        return new GlyphBounds(
            reader.I16(offset + 2) / em,
            reader.I16(offset + 4) / em,
            reader.I16(offset + 6) / em,
            reader.I16(offset + 8) / em);
    }

    private void AppendOutline(ushort glyph, List<PathCommand> commands, double a, double b, double c, double d, double dx, double dy, int depth)
    {
        var (offset, length) = GlyphData(glyph);
        if (length < 10 || depth > MaxCompositeDepth)
        {
            return;
        }

        var contours = reader.I16(offset);
        if (contours >= 0)
        {
            AppendSimple(offset, contours, commands, a, b, c, d, dx, dy);
            return;
        }

        var cursor = offset + 10;
        const int ArgsAreWords = 0x1, ArgsAreXy = 0x2, HasScale = 0x8, MoreComponents = 0x20, HasXyScale = 0x40, HasTwoByTwo = 0x80;
        int flags;
        do
        {
            flags = reader.U16(cursor);
            var component = reader.U16(cursor + 2);
            cursor += 4;
            double ox, oy;
            if ((flags & ArgsAreWords) != 0)
            {
                ox = reader.I16(cursor);
                oy = reader.I16(cursor + 2);
                cursor += 4;
            }
            else
            {
                ox = unchecked((sbyte)reader.U8(cursor));
                oy = unchecked((sbyte)reader.U8(cursor + 1));
                cursor += 2;
            }

            if ((flags & ArgsAreXy) == 0)
            {
                ox = oy = 0;
            }

            double ca = 1, cb = 0, cc = 0, cd = 1;
            if ((flags & HasScale) != 0)
            {
                ca = cd = F2Dot14(cursor);
                cursor += 2;
            }
            else if ((flags & HasXyScale) != 0)
            {
                ca = F2Dot14(cursor);
                cd = F2Dot14(cursor + 2);
                cursor += 4;
            }
            else if ((flags & HasTwoByTwo) != 0)
            {
                ca = F2Dot14(cursor);
                cb = F2Dot14(cursor + 2);
                cc = F2Dot14(cursor + 4);
                cd = F2Dot14(cursor + 6);
                cursor += 8;
            }

            var tx = a * ox + c * oy + dx * UnitsPerEm;
            var ty = b * ox + d * oy + dy * UnitsPerEm;
            AppendOutline(
                component,
                commands,
                a * ca + c * cb,
                b * ca + d * cb,
                a * cc + c * cd,
                b * cc + d * cd,
                tx / UnitsPerEm,
                ty / UnitsPerEm,
                depth + 1);
        }
        while ((flags & MoreComponents) != 0);
    }

    private double F2Dot14(int offset) => reader.I16(offset) / 16384.0;

    private void AppendSimple(int offset, int contours, List<PathCommand> commands, double a, double b, double c, double d, double dx, double dy)
    {
        var ends = new int[contours];
        for (var i = 0; i < contours; i++)
        {
            ends[i] = reader.U16(offset + 10 + i * 2);
        }

        var points = contours == 0 ? 0 : ends[^1] + 1;
        var cursor = offset + 10 + contours * 2;
        cursor += 2 + reader.U16(cursor);
        var flags = new byte[points];
        for (var i = 0; i < points;)
        {
            var flag = reader.U8(cursor++);
            flags[i++] = flag;
            if ((flag & 8) != 0)
            {
                var repeat = reader.U8(cursor++);
                for (var r = 0; r < repeat && i < points; r++)
                {
                    flags[i++] = flag;
                }
            }
        }

        var xs = new int[points];
        var ys = new int[points];
        var value = 0;
        for (var i = 0; i < points; i++)
        {
            var flag = flags[i];
            if ((flag & 2) != 0)
            {
                var delta = reader.U8(cursor++);
                value += (flag & 16) != 0 ? delta : -delta;
            }
            else if ((flag & 16) == 0)
            {
                value += reader.I16(cursor);
                cursor += 2;
            }

            xs[i] = value;
        }

        value = 0;
        for (var i = 0; i < points; i++)
        {
            var flag = flags[i];
            if ((flag & 4) != 0)
            {
                var delta = reader.U8(cursor++);
                value += (flag & 32) != 0 ? delta : -delta;
            }
            else if ((flag & 32) == 0)
            {
                value += reader.I16(cursor);
                cursor += 2;
            }

            ys[i] = value;
        }

        double em = UnitsPerEm;
        (double X, double Y) P(int i) => (
            (a * xs[i] + c * ys[i]) / em + dx,
            (b * xs[i] + d * ys[i]) / em + dy);

        var first = 0;
        foreach (var last in ends)
        {
            var count = last - first + 1;
            if (count < 2)
            {
                first = last + 1;
                continue;
            }

            bool On(int i) => (flags[first + (i % count + count) % count] & 1) != 0;
            (double X, double Y) Pt(int i) => P(first + (i % count + count) % count);

            var start = 0;
            while (start < count && !On(start))
            {
                start++;
            }

            (double X, double Y) startPoint = start < count
                ? Pt(start)
                : Mid(Pt(0), Pt(1));
            if (start == count)
            {
                start = 0;
            }

            commands.Add(new PathCommand(PathVerb.MoveTo, startPoint.X, startPoint.Y));
            (double X, double Y)? control = null;
            for (var k = 1; k <= count; k++)
            {
                var index = start + k;
                var point = Pt(index);
                if (On(index))
                {
                    commands.Add(control is { } q
                        ? new PathCommand(PathVerb.QuadTo, point.X, point.Y, q.X, q.Y)
                        : new PathCommand(PathVerb.LineTo, point.X, point.Y));
                    control = null;
                }
                else if (control is { } previous)
                {
                    var mid = Mid(previous, point);
                    commands.Add(new PathCommand(PathVerb.QuadTo, mid.X, mid.Y, previous.X, previous.Y));
                    control = point;
                }
                else
                {
                    control = point;
                }
            }

            if (control is { } tail)
            {
                commands.Add(new PathCommand(PathVerb.QuadTo, startPoint.X, startPoint.Y, tail.X, tail.Y));
            }

            commands.Add(new PathCommand(PathVerb.Close, 0, 0));
            first = last + 1;
        }
    }

    private static (double X, double Y) Mid((double X, double Y) p, (double X, double Y) q) => ((p.X + q.X) / 2, (p.Y + q.Y) / 2);
}
