namespace Workflow.Workflow.Engine.Core;

/// <summary>
/// Unified result pattern for all workflow operations providing consistent success/failure handling
/// </summary>
/// <typeparam name="T">The type of the result data</typeparam>
public class WorkflowOperationResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Result { get; init; }
    public string? ErrorMessage { get; init; }
    public Exception? Exception { get; init; }
    public DateTime OperationTime { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Creates a successful operation result
    /// </summary>
    public static WorkflowOperationResult<T> Success(T result, Dictionary<string, object>? metadata = null)
    {
        return new WorkflowOperationResult<T>
        {
            IsSuccess = true,
            Result = result,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Creates a failed operation result with error message
    /// </summary>
    public static WorkflowOperationResult<T> Failed(string errorMessage, Exception? exception = null,
        Dictionary<string, object>? metadata = null)
    {
        return new WorkflowOperationResult<T>
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Exception = exception,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Creates a failed operation result from exception
    /// </summary>
    public static WorkflowOperationResult<T> Failed(Exception exception, Dictionary<string, object>? metadata = null)
    {
        return new WorkflowOperationResult<T>
        {
            IsSuccess = false,
            ErrorMessage = exception.Message,
            Exception = exception,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Transforms the result to another type if successful
    /// </summary>
    public WorkflowOperationResult<TOut> Map<TOut>(Func<T, TOut> mapper)
    {
        if (!IsSuccess)
            return WorkflowOperationResult<TOut>.Failed(ErrorMessage ?? "Operation failed", Exception, Metadata);

        try
        {
            var mappedResult = mapper(Result!);
            return WorkflowOperationResult<TOut>.Success(mappedResult, Metadata);
        }
        catch (Exception ex)
        {
            return WorkflowOperationResult<TOut>.Failed($"Mapping failed: {ex.Message}", ex, Metadata);
        }
    }

    /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    public WorkflowOperationResult<T> OnSuccess(Action<T> onSuccess)
    {
        if (IsSuccess && Result != null) onSuccess(Result);
        return this;
    }

    /// <summary>
    /// Executes an action if the result is failed
    /// </summary>
    public WorkflowOperationResult<T> OnFailure(Action<string, Exception?> onFailure)
    {
        if (!IsSuccess) onFailure(ErrorMessage ?? "Operation failed", Exception);
        return this;
    }

    /// <summary>
    /// Gets the result or throws if failed
    /// </summary>
    public T GetResultOrThrow()
    {
        if (!IsSuccess) throw Exception ?? new InvalidOperationException(ErrorMessage ?? "Operation failed");
        return Result!;
    }

    /// <summary>
    /// Gets the result or returns default value
    /// </summary>
    public T? GetResultOrDefault(T? defaultValue = default)
    {
        return IsSuccess ? Result : defaultValue;
    }

    /// <summary>
    /// Adds metadata to the result
    /// </summary>
    public WorkflowOperationResult<T> WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Gets metadata value by key
    /// </summary>
    public TMetadata? GetMetadata<TMetadata>(string key)
    {
        if (Metadata.TryGetValue(key, out var value) && value is TMetadata typedValue)
            return typedValue;
        return default;
    }
}

/// <summary>
/// Non-generic version for operations that don't return data
/// </summary>
public class WorkflowOperationResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public Exception? Exception { get; init; }
    public DateTime OperationTime { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Creates a successful operation result
    /// </summary>
    public static WorkflowOperationResult Success(Dictionary<string, object>? metadata = null)
    {
        return new WorkflowOperationResult
        {
            IsSuccess = true,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Creates a failed operation result with error message
    /// </summary>
    public static WorkflowOperationResult Failed(string errorMessage, Exception? exception = null,
        Dictionary<string, object>? metadata = null)
    {
        return new WorkflowOperationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Exception = exception,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Creates a failed operation result from exception
    /// </summary>
    public static WorkflowOperationResult Failed(Exception exception, Dictionary<string, object>? metadata = null)
    {
        return new WorkflowOperationResult
        {
            IsSuccess = false,
            ErrorMessage = exception.Message,
            Exception = exception,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    public WorkflowOperationResult OnSuccess(Action onSuccess)
    {
        if (IsSuccess) onSuccess();
        return this;
    }

    /// <summary>
    /// Executes an action if the result is failed
    /// </summary>
    public WorkflowOperationResult OnFailure(Action<string, Exception?> onFailure)
    {
        if (!IsSuccess) onFailure(ErrorMessage ?? "Operation failed", Exception);
        return this;
    }

    /// <summary>
    /// Throws if the operation failed
    /// </summary>
    public void ThrowIfFailed()
    {
        if (!IsSuccess) throw Exception ?? new InvalidOperationException(ErrorMessage ?? "Operation failed");
    }

    /// <summary>
    /// Adds metadata to the result
    /// </summary>
    public WorkflowOperationResult WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Gets metadata value by key
    /// </summary>
    public TMetadata? GetMetadata<TMetadata>(string key)
    {
        if (Metadata.TryGetValue(key, out var value) && value is TMetadata typedValue)
            return typedValue;
        return default;
    }

    /// <summary>
    /// Converts to generic result type
    /// </summary>
    public WorkflowOperationResult<T> ToGeneric<T>(T result)
    {
        if (!IsSuccess)
            return WorkflowOperationResult<T>.Failed(ErrorMessage ?? "Operation failed", Exception, Metadata);
        return WorkflowOperationResult<T>.Success(result, Metadata);
    }
}

/// <summary>
/// Extension methods for WorkflowOperationResult
/// </summary>
public static class WorkflowOperationResultExtensions
{
    /// <summary>
    /// Combines multiple operation results into a single result
    /// </summary>
    public static WorkflowOperationResult Combine(params WorkflowOperationResult[] results)
    {
        var failedResults = results.Where(r => !r.IsSuccess).ToList();

        if (failedResults.Any())
        {
            var combinedErrorMessage = string.Join("; ", failedResults.Select(r => r.ErrorMessage));
            var combinedMetadata = results
                .SelectMany(r => r.Metadata)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return WorkflowOperationResult.Failed(combinedErrorMessage, null, combinedMetadata);
        }

        var successMetadata = results
            .SelectMany(r => r.Metadata)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return WorkflowOperationResult.Success(successMetadata);
    }

    /// <summary>
    /// Executes an async operation and returns the result wrapped in WorkflowOperationResult
    /// </summary>
    public static async Task<WorkflowOperationResult<T>> ExecuteAsync<T>(Func<Task<T>> operation,
        string? operationName = null)
    {
        try
        {
            var result = await operation();
            var metadata = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(operationName))
                metadata["OperationName"] = operationName;

            return WorkflowOperationResult<T>.Success(result, metadata);
        }
        catch (Exception ex)
        {
            var metadata = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(operationName))
                metadata["OperationName"] = operationName;

            return WorkflowOperationResult<T>.Failed(ex, metadata);
        }
    }

    /// <summary>
    /// Executes an async operation without return value and returns the result wrapped in WorkflowOperationResult
    /// </summary>
    public static async Task<WorkflowOperationResult> ExecuteAsync(Func<Task> operation, string? operationName = null)
    {
        try
        {
            await operation();
            var metadata = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(operationName))
                metadata["OperationName"] = operationName;

            return WorkflowOperationResult.Success(metadata);
        }
        catch (Exception ex)
        {
            var metadata = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(operationName))
                metadata["OperationName"] = operationName;

            return WorkflowOperationResult.Failed(ex, metadata);
        }
    }
}