using MkTeX.Fonts;
using MkTeX.Parsing;

namespace MkTeX.Layout;

internal sealed partial class Typesetter
{
    private Box LayoutOperator(OpNode op, Context ctx)
    {
        if (op.Body is not null)
        {
            return LayoutNode(op.Body, ctx);
        }

        var glyph = _font.GlyphIndex(op.CodePoint);
        if (glyph == 0)
        {
            return CenterOnAxis(FallbackText(char.ConvertFromUtf32(op.CodePoint), ctx, MathVariant.Normal), ctx);
        }

        if (ctx.IsDisplay && _font.Vertical(glyph) is { Variants.Count: > 0 } construction)
        {
            var min = _font.Constant(MathConstant.DisplayOperatorMinHeight);
            var chosen = construction.Variants[^1].Glyph;
            foreach (var variant in construction.Variants)
            {
                if (variant.Advance >= min)
                {
                    chosen = variant.Glyph;
                    break;
                }
            }

            glyph = chosen;
        }

        return CenterOnAxis(GlyphById(glyph, ctx), ctx);
    }

    private static bool UsesLimits(OpNode op, Context ctx) => op.LimitsOverride ?? (op.Limits && ctx.IsDisplay);

    private Box LayoutScripts(ScriptsNode node, Context ctx)
    {
        if (node.Nucleus is OpNode op && UsesLimits(op, ctx))
        {
            var opBox = LayoutOperator(op, ctx);
            var italic = op.Body is null ? ItalicOf(opBox) : 0;
            return LayoutLimits(opBox, node.Sup is null ? null : LayoutNode(node.Sup, ctx.Sup()), node.Sub is null ? null : LayoutNode(node.Sub, ctx.Sub()), italic, ctx);
        }

        var (nucleus, _) = LayoutAtom(node.Nucleus, ctx);

        // TeX boxes large operators before attaching scripts (make_op), so only plain characters skip the baseline drops.
        var isCharacter = node.Nucleus is CharNode;
        var isLargeOp = node.Nucleus is OpNode { Body: null };
        var italicCorrection = isCharacter || isLargeOp ? ItalicOf(nucleus) : 0;

        Box? primes = null;
        Node? supNode = node.Sup;
        if (node.Sup is PrimesNode p)
        {
            primes = LayoutPrimes(p.Count, ctx);
            supNode = p.Extra;
        }

        var sup = supNode is null ? null : LayoutNode(supNode, ctx.Sup());
        var sub = node.Sub is null ? null : LayoutNode(node.Sub, ctx.Sub());
        var supCtx = ctx.Sup();
        var subCtx = ctx.Sub();

        double supShift = 0, subShift = 0;
        if (sup is not null)
        {
            var drop = isCharacter ? 0 : nucleus.Height - supCtx[MathConstant.SuperscriptBaselineDropMax];
            supShift = Math.Max(
                Math.Max(ctx.Cramped ? ctx[MathConstant.SuperscriptShiftUpCramped] : ctx[MathConstant.SuperscriptShiftUp], drop),
                ctx[MathConstant.SuperscriptBottomMin] + sup.Depth);
        }

        if (sub is not null)
        {
            var drop = isCharacter ? 0 : nucleus.Depth + subCtx[MathConstant.SubscriptBaselineDropMin];
            subShift = Math.Max(
                Math.Max(ctx[MathConstant.SubscriptShiftDown], drop),
                sub.Height - ctx[MathConstant.SubscriptTopMax]);
        }

        if (sup is not null && sub is not null)
        {
            var gap = (supShift - sup.Depth) - (sub.Height - subShift);
            var minGap = ctx[MathConstant.SubSuperscriptGapMin];
            if (gap < minGap)
            {
                subShift += minGap - gap;
                var supBottomMax = ctx[MathConstant.SuperscriptBottomMaxWithSubscript];
                var supBottom = supShift - sup.Depth;
                if (supBottom < supBottomMax)
                {
                    var raise = Math.Min(supBottomMax - supBottom, subShift - Math.Max(ctx[MathConstant.SubscriptShiftDown], sub.Height - ctx[MathConstant.SubscriptTopMax]));
                    if (raise > 0)
                    {
                        supShift += raise;
                        subShift -= raise;
                    }
                }
            }
        }

        var space = ctx[MathConstant.SpaceAfterScript];
        var box = new ContainerBox();
        box.Add(nucleus, 0, 0);
        var x = nucleus.Width;
        var width = x;
        // OpenType large operators include the italic correction in their advance; letters do not.
        var supX = isLargeOp ? x : x + italicCorrection;
        if (primes is not null)
        {
            box.Add(primes, supX, 0);
            supX += primes.Width;
            width = Math.Max(width, supX);
        }

        if (sup is not null)
        {
            box.Add(sup, supX, -supShift);
            width = Math.Max(width, supX + sup.Width + space);
        }

        if (sub is not null)
        {
            var subX = x - (isLargeOp ? italicCorrection : 0);
            box.Add(sub, subX, subShift);
            width = Math.Max(width, subX + sub.Width + space);
        }

        if (sup is null && sub is null && primes is not null)
        {
            width = Math.Max(width, supX);
        }

        return box.Fit(width);
    }

    private Box LayoutPrimes(int count, Context ctx)
    {
        var row = new Row();
        var cp = count switch
        {
            2 => 0x2033,
            3 => 0x2034,
            _ => 0x2032
        };
        var remaining = count;
        if (count is 2 or 3 && Glyph(cp, ctx) is { } combined)
        {
            row.Add(combined);
            remaining = 0;
        }

        for (var i = 0; i < remaining; i++)
        {
            row.Add(Glyph(0x2032, ctx) ?? (Box)FallbackText("\u2032", ctx, MathVariant.Normal));
        }

        return row.Build();
    }

    private Box LayoutLimits(Box nucleus, Box? over, Box? under, double italic, Context ctx)
    {
        var width = Math.Max(nucleus.Width, Math.Max(over?.Width ?? 0, under?.Width ?? 0));
        var box = new ContainerBox();
        box.Add(nucleus, (width - nucleus.Width) / 2, 0);
        if (over is not null)
        {
            var gap = Math.Max(ctx[MathConstant.UpperLimitGapMin], ctx[MathConstant.UpperLimitBaselineRiseMin] - over.Depth);
            box.Add(over, (width - over.Width) / 2 + italic / 2, -(nucleus.Height + gap + over.Depth));
        }

        if (under is not null)
        {
            var gap = Math.Max(ctx[MathConstant.LowerLimitGapMin], ctx[MathConstant.LowerLimitBaselineDropMin] - under.Height);
            box.Add(under, (width - under.Width) / 2 - italic / 2, nucleus.Depth + gap + under.Height);
        }

        return box.Fit(width);
    }

    private static double ItalicOf(Box box) => box switch
    {
        GlyphBox g => g.Italic,
        ContainerBox { Children: [var only] } c when only.X == 0 && Math.Abs(only.Box.Width - c.Width) < 1e-9 => ItalicOf(only.Box),
        _ => 0
    };
}
