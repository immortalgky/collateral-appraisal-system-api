namespace Shared.Result;

public struct Maybe<T>
{
    private readonly T _value;
    public bool HasValue { get; }
    public bool HasNoValue => !HasValue;

    private Maybe(T value, bool hasValue)
    {
        _value = value;
        HasValue = hasValue;
    }

    public T Value
    {
        get
        {
            if (HasNoValue)
                throw new InvalidOperationException("Maybe has no value");
            return _value;
        }
    }

    public static Maybe<T> From(T value)
    {
        return value != null
            ? new Maybe<T>(value, true)
            : None;
    }

    public static Maybe<T> None => new(default, false);

    public Result<T> ToResult(string errorMessage)
    {
        return HasValue
            ? Result.Success(Value)
            : Result.Failure<T>(errorMessage, ErrorType.NotFound);
    }

    public T GetValueOrDefault(T defaultValue = default)
    {
        return HasValue ? Value : defaultValue;
    }

    public TResult Match<TResult>(
        Func<T, TResult> some,
        Func<TResult> none)
    {
        return HasValue ? some(Value) : none();
    }
}