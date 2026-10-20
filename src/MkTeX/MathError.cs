namespace MkTeX;

public enum MathErrorCode
{
    UnknownCommand,
    UnknownEnvironment,
    UnbalancedGroup,
    MismatchedEnvironment,
    MissingArgument,
    MissingDelimiter,
    InvalidDelimiter,
    InvalidArgument,
    UnexpectedToken,
    UnexpectedEnd,
    DoubleScript,
    TooLong,
    TooDeep,
    MacroLimit
}

/// <summary>A parse error with the offset of the offending token in the source.</summary>
public sealed record MathError(MathErrorCode Code, string Message, int Position);

public sealed class MathParseException(MathErrorCode code, string message, int position) : Exception(message)
{
    public MathErrorCode Code { get; } = code;

    public int Position { get; } = position;

    public MathError ToError() => new(Code, Message, Position);
}
