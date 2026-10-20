using MkTeX.Fonts;
using MkTeX.Parsing;

namespace MkTeX.Layout;

internal readonly record struct Context(MathStyleKind Style, bool Cramped, double Size, string? Color, MathFont Font)
{
    public double Scale => Size * Style switch
    {
        MathStyleKind.Script => Font.Constant(MathConstant.ScriptPercentScaleDown) / 100,
        MathStyleKind.ScriptScript => Font.Constant(MathConstant.ScriptScriptPercentScaleDown) / 100,
        _ => 1
    };

    public bool IsDisplay => Style == MathStyleKind.Display;

    public bool IsScript => Style >= MathStyleKind.Script;

    public double this[MathConstant constant] => Font.Constant(constant) * Scale;

    public double Axis => this[MathConstant.AxisHeight];

    public Context Sup() => this with { Style = Smaller(Style, script: true) };

    public Context Sub() => this with { Style = Smaller(Style, script: true), Cramped = true };

    public Context Numerator() => this with { Style = Smaller(Style, script: false) };

    public Context Denominator() => this with { Style = Smaller(Style, script: false), Cramped = true };

    public Context Cramp() => this with { Cramped = true };

    private static MathStyleKind Smaller(MathStyleKind style, bool script) => style switch
    {
        MathStyleKind.Display => script ? MathStyleKind.Script : MathStyleKind.Text,
        MathStyleKind.Text => MathStyleKind.Script,
        _ => MathStyleKind.ScriptScript
    };
}
