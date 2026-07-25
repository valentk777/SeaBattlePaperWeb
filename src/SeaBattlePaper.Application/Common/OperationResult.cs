namespace SeaBattlePaper.Application.Common;

public sealed record OperationError(string Code, string Message);

public class OperationResult
{
    public bool IsSuccess { get; init; }

    public OperationError? Error { get; init; }

    public static OperationResult Success() => new() { IsSuccess = true };

    public static OperationResult Failure(string code, string message) => new()
    {
        IsSuccess = false,
        Error = new OperationError(code, message)
    };
}

public sealed class OperationResult<T> : OperationResult
{
    public T? Value { get; init; }

    public static OperationResult<T> Success(T value) => new()
    {
        IsSuccess = true,
        Value = value
    };

    public new static OperationResult<T> Failure(string code, string message) => new()
    {
        IsSuccess = false,
        Error = new OperationError(code, message)
    };
}
