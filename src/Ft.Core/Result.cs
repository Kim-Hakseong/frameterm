namespace Ft.Core;

/// <summary>
/// Lightweight result type used across the core so parsing/IO failures
/// surface as values instead of exceptions (the RX loop must never die).
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;

    public bool IsOk { get; }
    public string Error { get; }

    public T Value => IsOk
        ? _value!
        : throw new InvalidOperationException($"Result is an error: {Error}");

    private Result(bool ok, T? value, string error)
    {
        IsOk = ok;
        _value = value;
        Error = error;
    }

    public static Result<T> Ok(T value) => new(true, value, string.Empty);
    public static Result<T> Fail(string error) => new(false, default, error);

    public Result<TOut> Map<TOut>(Func<T, TOut> f) =>
        IsOk ? Result<TOut>.Ok(f(Value)) : Result<TOut>.Fail(Error);
}
