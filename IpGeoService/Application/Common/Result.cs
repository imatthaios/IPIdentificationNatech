using System.Diagnostics.CodeAnalysis;

namespace Application.Common;

public sealed class Result<T>
{
    public T? Value { get; }

    public Error Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess { get; }

    [MemberNotNullWhen(false, nameof(Value))]
    public bool IsFailure => !IsSuccess;

    private Result(T value)
    {
        IsSuccess = true;
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Error = Error.None;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        Error = error ?? throw new ArgumentNullException(nameof(error));
        Value = default;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(string message, ErrorType type) =>
        new(new Error(message, type));

    public static Result<T> Failure(Error error) => new(error);
}