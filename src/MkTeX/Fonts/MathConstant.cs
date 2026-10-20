namespace MkTeX.Fonts;

/// <summary>OpenType MATH constants, in table order.</summary>
public enum MathConstant
{
    ScriptPercentScaleDown,
    ScriptScriptPercentScaleDown,
    DelimitedSubFormulaMinHeight,
    DisplayOperatorMinHeight,
    MathLeading,
    AxisHeight,
    AccentBaseHeight,
    FlattenedAccentBaseHeight,
    SubscriptShiftDown,
    SubscriptTopMax,
    SubscriptBaselineDropMin,
    SuperscriptShiftUp,
    SuperscriptShiftUpCramped,
    SuperscriptBottomMin,
    SuperscriptBaselineDropMax,
    SubSuperscriptGapMin,
    SuperscriptBottomMaxWithSubscript,
    SpaceAfterScript,
    UpperLimitGapMin,
    UpperLimitBaselineRiseMin,
    LowerLimitGapMin,
    LowerLimitBaselineDropMin,
    StackTopShiftUp,
    StackTopDisplayStyleShiftUp,
    StackBottomShiftDown,
    StackBottomDisplayStyleShiftDown,
    StackGapMin,
    StackDisplayStyleGapMin,
    StretchStackTopShiftUp,
    StretchStackBottomShiftDown,
    StretchStackGapAboveMin,
    StretchStackGapBelowMin,
    FractionNumeratorShiftUp,
    FractionNumeratorDisplayStyleShiftUp,
    FractionDenominatorShiftDown,
    FractionDenominatorDisplayStyleShiftDown,
    FractionNumeratorGapMin,
    FractionNumDisplayStyleGapMin,
    FractionRuleThickness,
    FractionDenominatorGapMin,
    FractionDenomDisplayStyleGapMin,
    SkewedFractionHorizontalGap,
    SkewedFractionVerticalGap,
    OverbarVerticalGap,
    OverbarRuleThickness,
    OverbarExtraAscender,
    UnderbarVerticalGap,
    UnderbarRuleThickness,
    UnderbarExtraDescender,
    RadicalVerticalGap,
    RadicalDisplayStyleVerticalGap,
    RadicalRuleThickness,
    RadicalExtraAscender,
    RadicalKernBeforeDegree,
    RadicalKernAfterDegree,
    RadicalDegreeBottomRaisePercent
}

public sealed record GlyphVariant(ushort Glyph, double Advance);

public sealed record GlyphPart(ushort Glyph, double StartConnector, double EndConnector, double FullAdvance, bool IsExtender);

/// <summary>Size variants of a stretchy glyph, smallest first, plus an optional assembly of parts (bottom-to-top or left-to-right).</summary>
public sealed record GlyphConstruction(IReadOnlyList<GlyphVariant> Variants, IReadOnlyList<GlyphPart> Assembly);
