using MkTeX.Fonts;
using MkTeX.Parsing;

namespace MkTeX.Layout;

internal sealed partial class Typesetter
{
    private const double DecorationGap = 0.1;
    private const double LineThickness = 0.04;

    private Box LayoutAccent(AccentNode accent, Context ctx)
    {
        var body = LayoutNode(accent.Body, ctx.Cramp());
        var scale = ctx.Scale;
        var box = new ContainerBox();
        if (accent.Stretchy)
        {
            var arrow = HorizontalStretch(accent.Accent, body.Width, ctx, keepVertical: false);
            var width = Math.Max(body.Width, arrow.Width);
            box.Add(body, (width - body.Width) / 2, 0);
            var y = accent.Under
                ? body.Depth + DecorationGap * scale + arrow.Height
                : -(body.Height + DecorationGap * scale);
            box.Add(arrow, (width - arrow.Width) / 2, y);
            return box.Fit(width);
        }

        var mark = HorizontalStretch(accent.Accent, accent.Wide ? body.Width : 0, ctx, keepVertical: true);
        var attach = body is GlyphBox glyph && _font.TopAccent(glyph.Glyph) is { } top ? top * glyph.Scale : body.Width / 2;
        box.Add(body, 0, 0);
        var shift = accent.Under
            ? body.Depth
            : -Math.Max(0, body.Height - ctx[MathConstant.AccentBaseHeight]);
        box.Add(mark, attach - mark.Width / 2, shift);
        return box.Fit(body.Width);
    }

    private Box LayoutBar(BarNode bar, Context ctx)
    {
        var body = LayoutNode(bar.Body, bar.Over ? ctx.Cramp() : ctx);
        var box = new ContainerBox();
        box.Add(body, 0, 0);
        if (bar.Over)
        {
            var gap = ctx[MathConstant.OverbarVerticalGap];
            var thickness = ctx[MathConstant.OverbarRuleThickness];
            var top = body.Height + gap + thickness;
            box.Add(Rule(body.Width, top, top - thickness, ctx.Color), 0, 0);
            box.Fit(body.Width);
            box.Height = top + ctx[MathConstant.OverbarExtraAscender];
        }
        else
        {
            var gap = ctx[MathConstant.UnderbarVerticalGap];
            var thickness = ctx[MathConstant.UnderbarRuleThickness];
            var bottom = body.Depth + gap + thickness;
            box.Add(Rule(body.Width, -(body.Depth + gap), -bottom, ctx.Color), 0, 0);
            box.Fit(body.Width);
            box.Depth = bottom + ctx[MathConstant.UnderbarExtraDescender];
        }

        return box;
    }

    private Box LayoutStack(StackNode stack, Context ctx)
    {
        if (stack.Base is null)
        {
            return LayoutExtensibleArrow(stack, ctx);
        }

        var body = LayoutNode(stack.Base, ctx);
        var over = stack.Over is null ? null : LayoutNode(stack.Over, ctx.Sup());
        var under = stack.Under is null ? null : LayoutNode(stack.Under, ctx.Sub());
        if (stack.Brace == BraceKind.None)
        {
            return LayoutLimits(body, over, under, 0, ctx);
        }

        var (cp, above) = stack.Brace switch
        {
            BraceKind.OverBrace => (0x23DE, true),
            BraceKind.UnderBrace => (0x23DF, false),
            BraceKind.OverBracket => (0x23B4, true),
            BraceKind.UnderBracket => (0x23B5, false),
            BraceKind.OverParen => (0x23DC, true),
            _ => (0x23DD, false)
        };
        var gap = DecorationGap * ctx.Scale;
        var brace = HorizontalStretch(cp, body.Width, ctx, keepVertical: false);
        var label = above ? over : under;
        var width = Math.Max(body.Width, Math.Max(brace.Width, label?.Width ?? 0));
        var box = new ContainerBox();
        box.Add(body, (width - body.Width) / 2, 0);
        if (above)
        {
            var braceBottom = body.Height + gap;
            box.Add(brace, (width - brace.Width) / 2, -braceBottom);
            if (label is not null)
            {
                box.Add(label, (width - label.Width) / 2, -(braceBottom + brace.Height + gap + label.Depth));
            }
        }
        else
        {
            var braceTop = body.Depth + gap;
            box.Add(brace, (width - brace.Width) / 2, braceTop + brace.Height);
            if (label is not null)
            {
                box.Add(label, (width - label.Width) / 2, braceTop + brace.Height + gap + label.Height);
            }
        }

        return box.Fit(width);
    }

    private Box LayoutExtensibleArrow(StackNode stack, Context ctx)
    {
        var scale = ctx.Scale;
        var over = stack.Over is null ? null : LayoutNode(stack.Over, ctx.Sup());
        var under = stack.Under is null ? null : LayoutNode(stack.Under, ctx.Sub());
        var content = Math.Max(over?.Width ?? 0, under?.Width ?? 0);
        var target = Math.Max(content + 0.6 * scale, 1.2 * scale);
        var arrow = stack.Brace switch
        {
            BraceKind.Equals => DoubleRule(target, ctx),
            BraceKind.Harpoons => Harpoons(target, ctx),
            _ => HorizontalStretch(ArrowCodePoint(stack.Brace), target, ctx, keepVertical: false)
        };

        var width = Math.Max(arrow.Width, content);
        var axis = ctx.Axis;
        var arrowBottom = axis - arrow.Height / 2;
        var gap = 0.111 * scale;
        var box = new ContainerBox();
        box.Add(arrow, (width - arrow.Width) / 2, -arrowBottom);
        if (over is not null)
        {
            box.Add(over, (width - over.Width) / 2, -(arrowBottom + arrow.Height + gap + over.Depth));
        }

        if (under is not null)
        {
            box.Add(under, (width - under.Width) / 2, -arrowBottom + gap + under.Height);
        }

        return box.Fit(width);
    }

    private static int ArrowCodePoint(BraceKind kind) => kind switch
    {
        BraceKind.ArrowLeft => 0x2190,
        BraceKind.ArrowLeftRight => 0x2194,
        BraceKind.DoubleRight => 0x21D2,
        BraceKind.DoubleLeft => 0x21D0,
        BraceKind.DoubleLeftRight => 0x21D4,
        BraceKind.HookRight => 0x21AA,
        BraceKind.HookLeft => 0x21A9,
        BraceKind.MapsTo => 0x21A6,
        _ => 0x2192
    };

    private Box DoubleRule(double width, Context ctx)
    {
        var thickness = ctx[MathConstant.FractionRuleThickness];
        var gap = 0.15 * ctx.Scale;
        var box = new ContainerBox();
        box.Add(Rule(width, thickness, 0, ctx.Color), 0, 0);
        box.Add(Rule(width, 2 * thickness + gap, thickness + gap, ctx.Color), 0, 0);
        return box.Fit(width);
    }

    private Box Harpoons(double width, Context ctx)
    {
        var top = HorizontalStretch(0x21C0, width, ctx, keepVertical: false);
        var bottom = HorizontalStretch(0x21BD, width, ctx, keepVertical: false);
        var w = Math.Max(top.Width, bottom.Width);
        var gap = 0.06 * ctx.Scale;
        var box = new ContainerBox();
        box.Add(bottom, (w - bottom.Width) / 2, 0);
        box.Add(top, (w - top.Width) / 2, -(bottom.Height + gap));
        return box.Fit(w);
    }

    private Box LayoutFramed(BoxNode node, Context ctx)
    {
        var body = LayoutNode(node.Body, ctx);
        var padding = node.Padding * ctx.Scale;
        var frame = new FrameBox(LineThickness * ctx.Scale, node.Kind != BoxKind.Color, node.Border ?? ctx.Color, node.Background)
        {
            Width = body.Width + 2 * padding,
            Height = body.Height + padding,
            Depth = body.Depth + padding
        };
        var box = new ContainerBox();
        box.Add(frame, 0, 0);
        box.Add(body, padding, 0);
        return box.Fit(frame.Width);
    }

    private Box LayoutPhantom(PhantomNode phantom, Context ctx)
    {
        var body = LayoutNode(phantom.Body, ctx);
        return new ContainerBox
        {
            Width = phantom.Width ? body.Width : 0,
            Height = phantom.Height ? body.Height : 0,
            Depth = phantom.Height ? body.Depth : 0
        };
    }

    private Box LayoutCancel(CancelNode cancel, Context ctx)
    {
        var body = LayoutNode(cancel.Body, ctx);
        var thickness = LineThickness * ctx.Scale;
        var box = new ContainerBox();
        box.Add(body, 0, 0);
        var w = body.Width;
        if (cancel.Kind is CancelKind.Forward or CancelKind.Cross)
        {
            box.Add(new LineBox(0, body.Depth, w, -body.Height, thickness, ctx.Color), 0, 0);
        }

        if (cancel.Kind is CancelKind.Back or CancelKind.Cross)
        {
            box.Add(new LineBox(0, -body.Height, w, body.Depth, thickness, ctx.Color), 0, 0);
        }

        if (cancel.Kind == CancelKind.Strike)
        {
            var y = -ctx.Axis;
            box.Add(new LineBox(0, y, w, y, thickness, ctx.Color), 0, 0);
        }

        return box.Fit(w);
    }

    private Box LayoutLap(LapNode lap, Context ctx)
    {
        var body = LayoutNode(lap.Body, ctx);
        var box = new ContainerBox();
        var x = lap.Kind switch
        {
            LapKind.Left => -body.Width,
            LapKind.Center => -body.Width / 2,
            _ => 0
        };
        box.Add(body, x, 0);
        return box.Fit(0);
    }

    private Box LayoutSmash(SmashNode smash, Context ctx)
    {
        var body = LayoutNode(smash.Body, ctx);
        var box = new ContainerBox();
        box.Add(body, 0, 0);
        box.Fit(body.Width);
        if (smash.Top)
        {
            box.Height = 0;
        }

        if (smash.Bottom)
        {
            box.Depth = 0;
        }

        return box;
    }

    private static Box LayoutRule(RuleNode rule, Context ctx)
    {
        var size = ctx.Size;
        return Rule(rule.Width * size, (rule.Raise + rule.Height) * size, rule.Raise * size, ctx.Color);
    }

    private Box LayoutRaise(RaiseNode raise, Context ctx) => Shift(LayoutNode(raise.Body, ctx), -raise.Amount * ctx.Size);

    private Box LayoutOverlay(OverlayNode overlay, Context ctx)
    {
        var body = LayoutNode(overlay.Base, ctx);
        var mark = LayoutNode(overlay.Overlay, ctx);
        var box = new ContainerBox();
        box.Add(body, 0, 0);
        box.Add(mark, (body.Width - mark.Width) / 2, 0);
        return box.Fit(body.Width);
    }
}
