using Microsoft.AspNetCore.Http;

namespace Shared.Result;

public static class ResultExtensions
{
    // Convert Result to IResult for minimal APIs
    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess)
            return Results.Ok();

        return result.ErrorType switch
        {
            ErrorType.Validation => Results.BadRequest(new { error = result.Error }),
            ErrorType.NotFound => Results.NotFound(new { error = result.Error }),
            ErrorType.Conflict => Results.Conflict(new { error = result.Error }),
            ErrorType.Unauthorized => Results.Unauthorized(),
            ErrorType.External => Results.Problem(
                result.Error,
                statusCode: StatusCodes.Status502BadGateway),
            ErrorType.System => Results.Problem(
                result.Error,
                statusCode: StatusCodes.Status500InternalServerError),
            _ => Results.Problem(result.Error)
        };
    }

    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, object> mapper = null)
    {
        if (result.IsSuccess)
        {
            var response = mapper != null ? mapper(result.Value) : result.Value;
            return Results.Ok(response);
        }

        return result.ErrorType switch
        {
            ErrorType.Validation => Results.BadRequest(new { error = result.Error }),
            ErrorType.NotFound => Results.NotFound(new { error = result.Error }),
            ErrorType.Conflict => Results.Conflict(new { error = result.Error }),
            ErrorType.Unauthorized => Results.Unauthorized(),
            ErrorType.External => Results.Problem(
                result.Error,
                statusCode: StatusCodes.Status502BadGateway),
            ErrorType.System => Results.Problem(
                result.Error,
                statusCode: StatusCodes.Status500InternalServerError),
            _ => Results.Problem(result.Error)
        };
    }

    // Async Result operations
    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Task<TOut>> mapper)
    {
        var result = await resultTask;
        if (result.IsFailure)
            return Result.Failure<TOut>(result.Error, result.ErrorType);

        try
        {
            var mapped = await mapper(result.Value);
            return Result.Success(mapped);
        }
        catch (Exception ex)
        {
            return Result.Failure<TOut>(ex.Message, ErrorType.System);
        }
    }

    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Task<Result<TOut>>> binder)
    {
        var result = await resultTask;
        if (result.IsFailure)
            return Result.Failure<TOut>(result.Error, result.ErrorType);

        try
        {
            return await binder(result.Value);
        }
        catch (Exception ex)
        {
            return Result.Failure<TOut>(ex.Message, ErrorType.System);
        }
    }

    // Ensure (validate result after success)
    public static Result<T> Ensure<T>(
        this Result<T> result,
        Func<T, bool> predicate,
        string error)
    {
        if (result.IsFailure)
            return result;

        return predicate(result.Value)
            ? result
            : Result.Failure<T>(error);
    }

    // Tap with async
    public static async Task<Result<T>> TapAsync<T>(
        this Result<T> result,
        Func<T, Task> action)
    {
        if (result.IsSuccess)
            await action(result.Value);
        return result;
    }
}