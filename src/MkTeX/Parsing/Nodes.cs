namespace MkTeX.Parsing;

internal abstract record Node;

internal sealed record ListNode(IReadOnlyList<Node> Items) : Node
{
    public static readonly ListNode Empty = new([]);
}

internal sealed record CharNode(int CodePoint, AtomType Type, MathVariant Variant, bool Text = false) : Node;

/// <summary>Named operator such as \sin, typeset in upright text.</summary>
internal sealed record OpNode(Node? Body, int CodePoint, bool Limits, bool? LimitsOverride = null) : Node;

internal sealed record ScriptsNode(Node Nucleus, Node? Sup, Node? Sub) : Node;

internal enum FracStyle
{
    Auto,
    Display,
    Text
}

internal sealed record FracNode(Node Numerator, Node Denominator, double? Thickness, FracStyle Style, int Left, int Right, bool Continued = false) : Node;

internal sealed record RadicalNode(Node Body, Node? Degree) : Node;

internal sealed record AccentNode(Node Body, int Accent, bool Wide, bool Under, bool Stretchy) : Node;

internal enum BraceKind
{
    None,
    OverBrace,
    UnderBrace,
    OverBracket,
    UnderBracket,
    OverParen,
    UnderParen,
    ArrowRight,
    ArrowLeft,
    ArrowLeftRight,
    DoubleRight,
    DoubleLeft,
    DoubleLeftRight,
    HookRight,
    HookLeft,
    MapsTo,
    Harpoons,
    Equals
}

/// <summary>Content stacked over and/or under a base (\overset, \underbrace, \xrightarrow).</summary>
internal sealed record StackNode(Node? Base, Node? Over, Node? Under, BraceKind Brace) : Node;

internal sealed record BarNode(Node Body, bool Over) : Node;

internal sealed record LeftRightNode(int Left, int Right, IReadOnlyList<Node> Parts, IReadOnlyList<int> Middles) : Node;

internal sealed record DelimNode(int CodePoint, double Size, AtomType Type) : Node;

/// <summary>Horizontal space in em; mu spaces are converted by the typesetter.</summary>
internal sealed record SpaceNode(double Amount, bool Mu, bool Breakable = false) : Node;

internal enum MathStyleKind
{
    Display,
    Text,
    Script,
    ScriptScript
}

internal sealed record StyleNode(MathStyleKind Style, Node Body) : Node;

internal sealed record ColorNode(string Color, Node Body) : Node;

internal sealed record SizeNode(double Scale, Node Body) : Node;

internal sealed record TextNode(string Text, MathVariant Variant) : Node;

internal enum ColumnAlign
{
    Left,
    Center,
    Right
}

internal sealed record ArrayNode(
    IReadOnlyList<IReadOnlyList<Node>> Rows,
    IReadOnlyList<ColumnAlign> Columns,
    IReadOnlyList<bool> ColumnLines,
    IReadOnlyList<bool> RowLines,
    double ColumnSpacing,
    double RowSpacing,
    bool AlignPairs,
    MathStyleKind? CellStyle,
    IReadOnlyList<string?> Tags) : Node;

internal enum BoxKind
{
    Frame,
    Color,
    FrameColor
}

internal sealed record BoxNode(Node Body, BoxKind Kind, string? Background, string? Border, double Padding) : Node;

internal sealed record PhantomNode(Node Body, bool Width, bool Height) : Node;

internal enum CancelKind
{
    Forward,
    Back,
    Cross,
    Strike
}

internal sealed record CancelNode(Node Body, CancelKind Kind) : Node;

internal sealed record ClassNode(AtomType Type, Node Body) : Node;

internal enum LapKind
{
    Left,
    Right,
    Center
}

internal sealed record LapNode(Node Body, LapKind Kind) : Node;

internal sealed record SmashNode(Node Body, bool Top, bool Bottom) : Node;

internal sealed record RuleNode(double Width, double Height, double Raise) : Node;

internal sealed record RaiseNode(double Amount, Node Body) : Node;

internal sealed record LineBreakNode : Node;

/// <summary>Primes after a nucleus, optionally followed by an explicit superscript.</summary>
internal sealed record PrimesNode(int Count, Node? Extra) : Node;

/// <summary>Draws <see cref="Overlay"/> centered over <see cref="Base"/> without taking width (\not).</summary>
internal sealed record OverlayNode(Node Base, Node Overlay) : Node;
