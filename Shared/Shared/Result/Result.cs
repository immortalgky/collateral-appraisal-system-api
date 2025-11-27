namespace Shared.Result;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }
    public ErrorType ErrorType { get; }

    protected Result(bool isSuccess, string error, ErrorType errorType = ErrorType.Validation)
    {
        if (isSuccess && !string.IsNullOrEmpty(error))
            throw new InvalidOperationException("A successful result cannot have an error message");

        if (IsFailure && string.IsNullOrEmpty(error))
            throw new InvalidOperationException("A failed result must have an error message");

        IsSuccess = isSuccess;
        Error = error;
        ErrorType = errorType;
    }

    public static Result Success()
    {
        return new Result(true, string.Empty);
    }

    public static Result Failure(string error, ErrorType errorType = ErrorType.Validation)
    {
        return new Result(false, error, errorType);
    }

    public static Result<T> Success<T>(T value)
    {
        return new Result<T>(value, true, string.Empty);
    }

    public static Result<T> Failure<T>(string error, ErrorType errorType = ErrorType.Validation)
    {
        return new Result<T>(default!, false, error, errorType);
    }

    // Combine multiple results
    public static Result Combine(params Result[] results)
    {
        foreach (var result in results)
            if (result.IsFailure)
                return result;

        return Success();
    }

    public static Result Combine(string separator, params Result[] results)
    {
        var errors = results
            .Where(r => r.IsFailure)
            .Select(r => r.Error)
            .ToArray();

        if (errors.Any())
            return Failure(string.Join(separator, errors));

        return Success();
    }
}

public class Result<T> : Result
{
    private readonly T _value;

    public T Value
    {
        get
        {
            if (IsFailure)
                throw new InvalidOperationException("Cannot access value of a failed result");

            return _value;
        }
    }

    protected internal Result(T value, bool isSuccess, string error, ErrorType errorType = ErrorType.Validation) : base(
        isSuccess, error, errorType)
    {
        _value = value;
    }

    // Implicit conversion from T to Result<T>
    public static implicit operator Result<T>(T value)
    {
        return Success(value);
    }

    // Map transformation
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        if (IsFailure)
            return Failure<TNew>(Error, ErrorType);

        try
        {
            return Success(mapper(Value));
        }
        catch (Exception ex)
        {
            return Failure<TNew>(ex.Message, ErrorType.System);
        }
    }

    // Bind (flatMap) for chaining
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> func)
    {
        if (IsFailure)
            return Failure<TNew>(Error, ErrorType);

        try
        {
            return func(Value);
        }
        catch (Exception ex)
        {
            return Failure<TNew>(ex.Message, ErrorType.System);
        }
    }

    // Match pattern
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(Value) : onFailure(Error);
    }

    // Tap (execute side effect without changing a result)
    public Result<T> Tap(Action<T> action)
    {
        if (IsSuccess)
            action(Value);

        return this;
    }

    // OnFailure (execute on failure)
    public Result<T> OnFailure(Action<string> action)
    {
        if (IsFailure)
            action(Error);

        return this;
    }
}

// Enhanced Result with an Error object
public class Result<T, TError> where TError : Error
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T Value { get; }
    public TError Error { get; }

    protected Result(T value, bool isSuccess, TError error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T, TError> Success(T value)
    {
        return new Result<T, TError>(value, true, default!);
    }

    public static Result<T, TError> Failure(TError error)
    {
        return new Result<T, TError>(default!, false, error);
    }

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<TError, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(Value) : onFailure(Error);
    }
}