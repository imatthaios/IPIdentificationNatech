namespace Application.Common;

public sealed record Error(string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, ErrorType.None);

    public static Error Validation(string message) =>
        new(message, ErrorType.Validation);

    public static Error NotFound(string message) =>
        new(message, ErrorType.NotFound);

    public static Error Conflict(string message) =>
        new(message, ErrorType.Conflict);

    public static Error Unexpected(string message) =>
        new(message, ErrorType.Unexpected);
}