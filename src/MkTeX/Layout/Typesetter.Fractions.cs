using MkTeX.Fonts;
using MkTeX.Parsing;

namespace MkTeX.Layout;

internal sealed partial class Typesetter
{
    private Box LayoutFraction(FracNode frac, Context outer)
    {
        var ctx = frac.Style switch
        {
            FracStyle.Display => outer with { Style = MathStyleKind.Display, Cramped = false },
            FracStyle.Text => outer with { Style = MathStyleKind.Text, Cramped = false },
            _ => outer
        };

        var numCtx = frac.Continued ? ctx : ctx.Numerator();
        var denCtx = frac.Continued ? ctx.Cramp() : ctx.Denominator();
        var num = LayoutNode(frac.Numerator, numCtx);
        var den = LayoutNode(frac.Denominator, denCtx);
        if (frac.Continued)
        {
            num = Strut(num, ctx);
        }

        var display = ctx.IsDisplay;
        var thickness = frac.Thickness is { } t ? t * ctx.Size : ctx[MathConstant.FractionRuleThickness];
        var axis = ctx.Axis;
        double numShift, denShift;
        if (thickness > 0)
        {
            numShift = display ? ctx[MathConstant.FractionNumeratorDisplayStyleShiftUp] : ctx[MathConstant.FractionNumeratorShiftUp];
            denShift = display ? ctx[MathConstant.FractionDenominatorDisplayStyleShiftDown] : ctx[MathConstant.FractionDenominatorShiftDown];
            var numGap = display ? ctx[MathConstant.FractionNumDisplayStyleGapMin] : ctx[MathConstant.FractionNumeratorGapMin];
            var denGap = display ? ctx[MathConstant.FractionDenomDisplayStyleGapMin] : ctx[MathConstant.FractionDenominatorGapMin];
            numShift = Math.Max(numShift, numGap + axis + thickness / 2 + num.Depth);
            denShift = Math.Max(denShift, denGap + thickness / 2 - axis + den.Height);
        }
        else
        {
            numShift = display ? ctx[MathConstant.StackTopDisplayStyleShiftUp] : ctx[MathConstant.StackTopShiftUp];
            denShift = display ? ctx[MathConstant.StackBottomDisplayStyleShiftDown] : ctx[MathConstant.StackBottomShiftDown];
            var minGap = display ? ctx[MathConstant.StackDisplayStyleGapMin] : ctx[MathConstant.StackGapMin];
            var gap = (numShift - num.Depth) - (den.Height - denShift);
            if (gap < minGap)
            {
                numShift += (minGap - gap) / 2;
                denShift += (minGap - gap) / 2;
            }
        }

        var width = Math.Max(num.Width, den.Width);
        var box = new ContainerBox();
        box.Add(num, (width - num.Width) / 2, -numShift);
        box.Add(den, (width - den.Width) / 2, denShift);
        if (thickness > 0)
        {
            box.Add(Rule(width, axis + thickness / 2, axis - thickness / 2, ctx.Color), 0, 0);
        }

        box.Fit(width);

        if (frac.Left == 0 && frac.Right == 0)
        {
            var padded = new Row();
            padded.Kern(NullDelimiterSpace * ctx.Scale);
            padded.Add(box);
            padded.Kern(NullDelimiterSpace * ctx.Scale);
            return padded.Build();
        }

        // TeX \delim1 / \delim2 for generalized fractions.
        var size = (display ? 2.39 : 1.01) * ctx.Scale;
        var row = new Row();
        row.Add(Delimiter(frac.Left, size, ctx));
        row.Add(box);
        row.Add(Delimiter(frac.Right, size, ctx));
        return row.Build();
    }

    private static Box Strut(Box box, Context ctx)
    {
        var scale = ctx.Scale;
        if (box.Height >= 0.85 * scale && box.Depth >= 0.35 * scale)
        {
            return box;
        }

        var container = new ContainerBox();
        container.Add(box, 0, 0);
        container.Fit(box.Width);
        container.Height = Math.Max(container.Height, 0.85 * scale);
        container.Depth = Math.Max(container.Depth, 0.35 * scale);
        return container;
    }

    private Box LayoutRadical(RadicalNode radical, Context ctx)
    {
        var body = LayoutNode(radical.Body, ctx.Cramp());
        var thickness = ctx[MathConstant.RadicalRuleThickness];
        var gap = ctx.IsDisplay ? ctx[MathConstant.RadicalDisplayStyleVerticalGap] : ctx[MathConstant.RadicalVerticalGap];
        var extra = ctx[MathConstant.RadicalExtraAscender];
        var target = body.Height + body.Depth + gap + thickness;
        var surd = VerticalStretch(0x221A, target, ctx);
        var surdTotal = surd.Height + surd.Depth;
        if (surdTotal > target)
        {
            gap += (surdTotal - target) / 2;
        }

        var ruleTop = body.Height + gap + thickness;
        var surdShift = surd.Height - ruleTop;

        var box = new ContainerBox();
        var x = 0.0;
        if (radical.Degree is not null)
        {
            var degreeCtx = ctx with { Style = MathStyleKind.ScriptScript, Cramped = false };
            var degree = LayoutNode(radical.Degree, degreeCtx);
            var before = ctx[MathConstant.RadicalKernBeforeDegree];
            var after = ctx[MathConstant.RadicalKernAfterDegree];
            var raise = _font.Constant(MathConstant.RadicalDegreeBottomRaisePercent) / 100 * surdTotal;
            var surdBottom = ruleTop - surdTotal;
            box.Add(degree, before, -(surdBottom + raise));
            x = Math.Max(0, before + degree.Width + after);
        }

        box.Add(surd, x, surdShift);
        x += surd.Width;
        box.Add(Rule(body.Width, ruleTop, ruleTop - thickness, ctx.Color), x, 0);
        box.Add(body, x, 0);
        box.Fit(x + body.Width);
        box.Height = Math.Max(box.Height, ruleTop + extra);
        return box;
    }
}
