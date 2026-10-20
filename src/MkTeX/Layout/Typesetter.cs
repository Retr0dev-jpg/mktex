using System.Text;
using MkTeX.Fonts;
using MkTeX.Parsing;

namespace MkTeX.Layout;

/// <summary>Turns parse trees into boxes following TeX's Appendix G, driven by OpenType MATH constants.</summary>
internal sealed partial class Typesetter
{
    // TeX inter-atom spacing: rows = left atom, columns = right atom (Ord Op Bin Rel Open Close Punct Inner).
    // Digits are unconditional thin/med/thick spaces; a/b/c are the same but suppressed in script styles.
    private static readonly string[] SpacingTable =
    [
        "01bc000a",
        "11*c000a",
        "bb**b**b",
        "cc*0c00c",
        "00*00000",
        "01bc000a",
        "aa*aaaaa",
        "a1bca0aa"
    ];

    private const double NullDelimiterSpace = 0.12;

    private readonly MathFont _font;

    public Typesetter(MathFont font)
    {
        _font = font;
    }

    private sealed record Atom(Box Box, AtomType Type, Context Context, bool IsSpace = false)
    {
        public AtomType Type { get; set; } = Type;
    }

    public Box LayoutRoot(ListNode root, Context ctx)
    {
        if (!root.Items.Any(n => n is LineBreakNode))
        {
            return LayoutList(root.Items, ctx);
        }

        var lines = new List<List<Node>> { new() };
        foreach (var item in root.Items)
        {
            if (item is LineBreakNode)
            {
                lines.Add([]);
            }
            else
            {
                lines[^1].Add(item);
            }
        }

        var rows = lines.Select(l => (IReadOnlyList<Node>)[new ListNode(l)]).ToList();
        var array = new ArrayNode(
            rows,
            [ColumnAlign.Center],
            [false, false],
            Enumerable.Repeat(false, rows.Count + 1).ToList(),
            0,
            1,
            false,
            ctx.Style,
            Enumerable.Repeat<string?>(null, rows.Count).ToList());
        return LayoutArray(array, ctx);
    }

    public Box LayoutNode(Node node, Context ctx) => node is ListNode list ? LayoutList(list.Items, ctx) : LayoutList([node], ctx);

    public Box LayoutList(IReadOnlyList<Node> nodes, Context ctx)
    {
        var atoms = new List<Atom>();
        CollectAtoms(nodes, ctx, atoms);
        if (atoms.Count == 1 && !atoms[0].IsSpace)
        {
            return atoms[0].Box;
        }

        AdjustBinaries(atoms);
        var row = new Row();
        Atom? previous = null;
        foreach (var atom in atoms)
        {
            if (atom.IsSpace)
            {
                row.Add(atom.Box);
                continue;
            }

            if (previous is not null)
            {
                row.Kern(Spacing(previous.Type, atom.Type, atom.Context));
            }

            row.Add(atom.Box);
            previous = atom;
        }

        return row.Build();
    }

    private void CollectAtoms(IReadOnlyList<Node> nodes, Context ctx, List<Atom> atoms)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case StyleNode style:
                    CollectAtoms([style.Body], ctx with { Style = style.Style, Cramped = false }, atoms);
                    break;
                case SizeNode size:
                    CollectAtoms([size.Body], ctx with { Size = size.Scale }, atoms);
                    break;
                case ColorNode color:
                    CollectAtoms([color.Body], ctx with { Color = color.Color }, atoms);
                    break;
                case ListNode { Items.Count: 1 } list when list.Items[0] is StyleNode or SizeNode or ColorNode:
                    CollectAtoms(list.Items, ctx, atoms);
                    break;
                case SpaceNode space:
                    atoms.Add(new Atom(new ContainerBox { Width = SpaceWidth(space, ctx) }, AtomType.Ord, ctx, IsSpace: true));
                    break;
                case LineBreakNode:
                    break;
                default:
                    var (box, type) = LayoutAtom(node, ctx);
                    atoms.Add(new Atom(box, type, ctx));
                    break;
            }
        }
    }

    private static double SpaceWidth(SpaceNode space, Context ctx) => space.Mu ? space.Amount / 18 * ctx.Scale : space.Amount * ctx.Size;

    private static void AdjustBinaries(List<Atom> atoms)
    {
        Atom? previous = null;
        foreach (var atom in atoms)
        {
            if (atom.IsSpace)
            {
                continue;
            }

            if (atom.Type == AtomType.Bin &&
                (previous is null || previous.Type is AtomType.Bin or AtomType.Op or AtomType.Rel or AtomType.Open or AtomType.Punct))
            {
                atom.Type = AtomType.Ord;
            }

            if (previous is { Type: AtomType.Bin } && atom.Type is AtomType.Rel or AtomType.Close or AtomType.Punct)
            {
                previous.Type = AtomType.Ord;
            }

            previous = atom;
        }

        if (previous is { Type: AtomType.Bin })
        {
            previous.Type = AtomType.Ord;
        }
    }

    private static double Spacing(AtomType left, AtomType right, Context ctx)
    {
        var code = SpacingTable[(int)left][(int)right];
        var mu = code switch
        {
            '1' => 3,
            '2' => 4,
            '3' => 5,
            'a' when !ctx.IsScript => 3,
            'b' when !ctx.IsScript => 4,
            'c' when !ctx.IsScript => 5,
            _ => 0
        };
        return mu / 18.0 * ctx.Scale;
    }

    private (Box Box, AtomType Type) LayoutAtom(Node node, Context ctx) => node switch
    {
        CharNode c => (LayoutChar(c, ctx), c.Type),
        ListNode list => (LayoutList(list.Items, ctx), AtomType.Ord),
        OpNode op => (LayoutOperator(op, ctx), AtomType.Op),
        ScriptsNode scripts => (LayoutScripts(scripts, ctx), NucleusType(scripts.Nucleus)),
        FracNode frac => (LayoutFraction(frac, ctx), AtomType.Inner),
        RadicalNode radical => (LayoutRadical(radical, ctx), AtomType.Ord),
        AccentNode accent => (LayoutAccent(accent, ctx), AtomType.Ord),
        StackNode stack => (LayoutStack(stack, ctx), AtomType.Ord),
        BarNode bar => (LayoutBar(bar, ctx), AtomType.Ord),
        LeftRightNode lr => (LayoutLeftRight(lr, ctx), AtomType.Inner),
        DelimNode delim => (LayoutSizedDelimiter(delim, ctx), delim.Type),
        TextNode text => (LayoutText(text, ctx), AtomType.Ord),
        ArrayNode array => (LayoutArray(array, ctx), AtomType.Ord),
        BoxNode box => (LayoutFramed(box, ctx), AtomType.Ord),
        PhantomNode phantom => (LayoutPhantom(phantom, ctx), AtomType.Ord),
        CancelNode cancel => (LayoutCancel(cancel, ctx), AtomType.Ord),
        ClassNode cls => (LayoutNode(cls.Body, ctx), cls.Type),
        LapNode lap => (LayoutLap(lap, ctx), AtomType.Ord),
        SmashNode smash => (LayoutSmash(smash, ctx), AtomType.Ord),
        RuleNode rule => (LayoutRule(rule, ctx), AtomType.Ord),
        RaiseNode raise => (LayoutRaise(raise, ctx), AtomType.Ord),
        OverlayNode overlay => (LayoutOverlay(overlay, ctx), AtomType.Ord),
        PrimesNode primes => (LayoutPrimes(primes.Count, ctx), AtomType.Ord),
        StyleNode or SizeNode or ColorNode => (LayoutList([node], ctx), AtomType.Ord),
        _ => (new ContainerBox(), AtomType.Ord)
    };

    private static AtomType NucleusType(Node nucleus) => nucleus switch
    {
        CharNode c => c.Type,
        OpNode => AtomType.Op,
        ClassNode c => c.Type,
        DelimNode d => d.Type,
        FracNode or LeftRightNode => AtomType.Inner,
        _ => AtomType.Ord
    };

    private Box LayoutChar(CharNode c, Context ctx)
    {
        var mapped = Alphabets.Map(c.CodePoint, c.Variant);
        return (Box?)Glyph(mapped, ctx) ?? Glyph(c.CodePoint, ctx) ?? (Box)FallbackText(char.ConvertFromUtf32(c.CodePoint), ctx, c.Variant);
    }

    /// <summary>A glyph box for a code point, or null when the font lacks it.</summary>
    private GlyphBox? Glyph(int codePoint, Context ctx)
    {
        var glyph = _font.GlyphIndex(codePoint);
        return glyph == 0 ? null : GlyphById(glyph, ctx);
    }

    private GlyphBox GlyphById(ushort glyph, Context ctx)
    {
        var scale = ctx.Scale;
        var bounds = _font.Bounds(glyph);
        return new GlyphBox(glyph, scale, _font.Italic(glyph) * scale, ctx.Color)
        {
            Width = _font.Advance(glyph) * scale,
            Height = bounds.YMax * scale,
            Depth = -bounds.YMin * scale
        };
    }

    private static TextBox FallbackText(string text, Context ctx, MathVariant variant)
    {
        var scale = ctx.Scale;
        var width = 0.0;
        foreach (var rune in text.EnumerateRunes())
        {
            width += rune.Value < 0x2E80 ? 0.55 : 1.0;
        }

        return new TextBox(
            text,
            scale,
            variant is MathVariant.Bold or MathVariant.BoldItalic or MathVariant.SansSerifBold or MathVariant.SansSerifBoldItalic,
            variant is MathVariant.Italic or MathVariant.BoldItalic or MathVariant.Default or MathVariant.SansSerifItalic,
            ctx.Color)
        {
            Width = width * scale,
            Height = 0.72 * scale,
            Depth = 0.22 * scale
        };
    }

    private Box LayoutText(TextNode text, Context ctx)
    {
        var row = new Row();
        var pending = new StringBuilder();

        void FlushFallback()
        {
            if (pending.Length > 0)
            {
                row.Add(FallbackText(pending.ToString(), ctx, text.Variant));
                pending.Clear();
            }
        }

        foreach (var rune in text.Text.EnumerateRunes())
        {
            var cp = rune.Value;
            if (cp is ' ' or 0xA0)
            {
                FlushFallback();
                var space = _font.GlyphIndex(' ');
                row.Kern((space == 0 ? 0.25 : _font.Advance(space)) * ctx.Scale);
                continue;
            }

            var mapped = text.Variant == MathVariant.Normal ? cp : Alphabets.Map(cp, text.Variant);
            var glyph = Glyph(mapped, ctx) ?? (mapped != cp ? Glyph(cp, ctx) : null);
            if (glyph is null)
            {
                pending.Append(rune.ToString());
                continue;
            }

            FlushFallback();
            row.Add(glyph);
        }

        FlushFallback();
        return row.Build();
    }

    private static ContainerBox Shift(Box box, double down)
    {
        var container = new ContainerBox();
        container.Add(box, 0, down);
        return container.Fit(box.Width);
    }

    private ContainerBox CenterOnAxis(Box box, Context ctx) => Shift(box, (box.Height - box.Depth) / 2 - ctx.Axis);

    private static RuleBox Rule(double width, double top, double bottom, string? color) => new(color)
    {
        Width = width,
        Height = top,
        Depth = -bottom
    };

    private static ContainerBox Kern(double width) => new() { Width = width };
}
