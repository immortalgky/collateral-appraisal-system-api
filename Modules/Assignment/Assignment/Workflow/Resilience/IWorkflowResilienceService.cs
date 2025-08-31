using Assignment.Workflow.Activities.Core;

namespace Assignment.Workflow.Resilience;

/// <summary>
/// Service for handling workflow resilience patterns including circuit breakers, retries, and timeouts
/// Ensures workflow operations can gracefully handle failures and recover automatically
/// </summary>
public interface IWorkflowResilienceService
{
    /// <summary>
    /// Executes an operation with retry policy and circuit breaker protection
    /// </summary>
    Task<T> ExecuteWithResilienceAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> operation,
        ResiliencePolicy? policy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation that doesn't return a value with resilience patterns
    /// </summary>
    Task ExecuteWithResilienceAsync(
        string operationName,
        Func<CancellationToken, Task> operation,
        ResiliencePolicy? policy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an external service call with fallback handling
    /// </summary>
    Task<T> ExecuteExternalServiceCallAsync<T>(
        string serviceName,
        Func<CancellationToken, Task<T>> serviceCall,
        Func<Exception, T>? fallbackProvider = null,
        ResiliencePolicy? policy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current health status of a circuit breaker for a service
    /// </summary>
    CircuitBreakerState GetCircuitBreakerState(string operationName);

    /// <summary>
    /// Manually opens a circuit breaker (useful for maintenance or known issues)
    /// </summary>
    Task OpenCircuitBreakerAsync(string operationName, TimeSpan? duration = null);

    /// <summary>
    /// Manually closes a circuit breaker
    /// </summary>
    Task CloseCircuitBreakerAsync(string operationName);

    /// <summary>
    /// Gets resilience metrics for monitoring and alerting
    /// </summary>
    ResilienceMetrics GetResilienceMetrics(string? operationName = null);

    /// <summary>
    /// Validates that an operation can be executed (not blocked by circuit breakers)
    /// </summary>
    Task<OperationValidationResult> ValidateOperationAsync(
        string operationName, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resilience policy configuration for operations
/// </summary>
public class ResiliencePolicy
{
    /// <summary>
    /// Retry policy configuration
    /// </summary>
    public RetryPolicy RetryPolicy { get; init; } = new();

    /// <summary>
    /// Circuit breaker configuration
    /// </summary>
    public CircuitBreakerPolicy CircuitBreakerPolicy { get; init; } = new();

    /// <summary>
    /// Timeout configuration
    /// </summary>
    public TimeoutPolicy TimeoutPolicy { get; init; } = new();

    /// <summary>
    /// Bulkhead isolation configuration
    /// </summary>
    public BulkheadPolicy? BulkheadPolicy { get; init; }

    /// <summary>
    /// Whether to enable fallback mechanisms
    /// </summary>
    public bool EnableFallback { get; init; } = true;

    /// <summary>
    /// Default policy for workflow operations
    /// </summary>
    public static ResiliencePolicy Default => new()
    {
        RetryPolicy = RetryPolicy.Default,
        CircuitBreakerPolicy = CircuitBreakerPolicy.Default,
        TimeoutPolicy = TimeoutPolicy.Default,
        EnableFallback = true
    };

    /// <summary>
    /// Aggressive policy for critical operations
    /// </summary>
    public static ResiliencePolicy Aggressive => new()
    {
        RetryPolicy = RetryPolicy.Aggressive,
        CircuitBreakerPolicy = CircuitBreakerPolicy.Sensitive,
        TimeoutPolicy = TimeoutPolicy.Short,
        EnableFallback = true
    };

    /// <summary>
    /// Lenient policy for non-critical operations
    /// </summary>
    public static ResiliencePolicy Lenient => new()
    {
        RetryPolicy = RetryPolicy.Lenient,
        CircuitBreakerPolicy = CircuitBreakerPolicy.Tolerant,
        TimeoutPolicy = TimeoutPolicy.Long,
        EnableFallback = false
    };
}

/// <summary>
/// Retry policy configuration
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Base delay between retries
    /// </summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Backoff strategy for retry delays
    /// </summary>
    public BackoffStrategy BackoffStrategy { get; init; } = BackoffStrategy.Exponential;

    /// <summary>
    /// Maximum delay between retries
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Exception types that should trigger retries
    /// </summary>
    public List<Type> RetriableExceptions { get; init; } = new()
    {
        typeof(TimeoutException),
        typeof(HttpRequestException),
        typeof(TaskCanceledException)
    };

    /// <summary>
    /// Exception types that should NOT trigger retries
    /// </summary>
    public List<Type> NonRetriableExceptions { get; init; } = new()
    {
        typeof(ArgumentException),
        typeof(ArgumentNullException),
        typeof(InvalidOperationException)
    };

    /// <summary>
    /// Default retry policy
    /// </summary>
    public static RetryPolicy Default => new();

    /// <summary>
    /// Aggressive retry policy for critical operations
    /// </summary>
    public static RetryPolicy Aggressive => new()
    {
        MaxRetryAttempts = 5,
        BaseDelay = TimeSpan.FromMilliseconds(500),
        BackoffStrategy = BackoffStrategy.ExponentialWithJitter,
        MaxDelay = TimeSpan.FromSeconds(10)
    };

    /// <summary>
    /// Lenient retry policy for non-critical operations
    /// </summary>
    public static RetryPolicy Lenient => new()
    {
        MaxRetryAttempts = 2,
        BaseDelay = TimeSpan.FromSeconds(2),
        BackoffStrategy = BackoffStrategy.Linear,
        MaxDelay = TimeSpan.FromMinutes(1)
    };
}

/// <summary>
/// Circuit breaker policy configuration
/// </summary>
public class CircuitBreakerPolicy
{
    /// <summary>
    /// Number of failures before opening the circuit
    /// </summary>
    public int FailureThreshold { get; init; } = 5;

    /// <summary>
    /// Time window for counting failures
    /// </summary>
    public TimeSpan SamplingWindow { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Duration to keep circuit open before trying again
    /// </summary>
    public TimeSpan OpenCircuitDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Minimum number of requests in sampling window before circuit can open
    /// </summary>
    public int MinimumThroughput { get; init; } = 10;

    /// <summary>
    /// Success rate threshold to close the circuit (0.0 to 1.0)
    /// </summary>
    public double SuccessThreshold { get; init; } = 0.8;

    /// <summary>
    /// Default circuit breaker policy
    /// </summary>
    public static CircuitBreakerPolicy Default => new();

    /// <summary>
    /// Sensitive circuit breaker that opens quickly
    /// </summary>
    public static CircuitBreakerPolicy Sensitive => new()
    {
        FailureThreshold = 3,
        SamplingWindow = TimeSpan.FromSeconds(30),
        OpenCircuitDuration = TimeSpan.FromSeconds(15),
        MinimumThroughput = 5,
        SuccessThreshold = 0.9
    };

    /// <summary>
    /// Tolerant circuit breaker for less critical operations
    /// </summary>
    public static CircuitBreakerPolicy Tolerant => new()
    {
        FailureThreshold = 10,
        SamplingWindow = TimeSpan.FromMinutes(5),
        OpenCircuitDuration = TimeSpan.FromMinutes(2),
        MinimumThroughput = 20,
        SuccessThreshold = 0.6
    };
}

/// <summary>
/// Timeout policy configuration
/// </summary>
public class TimeoutPolicy
{
    /// <summary>
    /// Operation timeout duration
    /// </summary>
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to use pessimistic or optimistic timeout
    /// </summary>
    public TimeoutStrategy TimeoutStrategy { get; init; } = TimeoutStrategy.Optimistic;

    /// <summary>
    /// Default timeout policy
    /// </summary>
    public static TimeoutPolicy Default => new();

    /// <summary>
    /// Short timeout for fast operations
    /// </summary>
    public static TimeoutPolicy Short => new()
    {
        OperationTimeout = TimeSpan.FromSeconds(10),
        TimeoutStrategy = TimeoutStrategy.Pessimistic
    };

    /// <summary>
    /// Long timeout for complex operations
    /// </summary>
    public static TimeoutPolicy Long => new()
    {
        OperationTimeout = TimeSpan.FromMinutes(5),
        TimeoutStrategy = TimeoutStrategy.Optimistic
    };
}

/// <summary>
/// Bulkhead isolation policy configuration
/// </summary>
public class BulkheadPolicy
{
    /// <summary>
    /// Maximum number of concurrent executions
    /// </summary>
    public int MaxConcurrency { get; init; } = 10;

    /// <summary>
    /// Maximum queue length for waiting operations
    /// </summary>
    public int MaxQueueLength { get; init; } = 100;

    /// <summary>
    /// Timeout for queued operations
    /// </summary>
    public TimeSpan QueueTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Backoff strategies for retry delays
/// </summary>
public enum BackoffStrategy
{
    Linear,
    Exponential,
    ExponentialWithJitter,
    Fixed
}

/// <summary>
/// Timeout strategies
/// </summary>
public enum TimeoutStrategy
{
    /// <summary>
    /// Optimistic timeout - allows operation to complete naturally
    /// </summary>
    Optimistic,
    
    /// <summary>
    /// Pessimistic timeout - actively cancels operation
    /// </summary>
    Pessimistic
}

/// <summary>
/// Circuit breaker states
/// </summary>
public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen,
    Disabled
}

/// <summary>
/// Result of operation validation
/// </summary>
public class OperationValidationResult
{
    public bool CanExecute { get; init; }
    public string? BlockReason { get; init; }
    public CircuitBreakerState CircuitBreakerState { get; init; }
    public TimeSpan? EstimatedWaitTime { get; init; }

    public static OperationValidationResult Allowed => new()
    {
        CanExecute = true,
        CircuitBreakerState = CircuitBreakerState.Closed
    };

    public static OperationValidationResult Blocked(string reason, CircuitBreakerState state, TimeSpan? waitTime = null) => new()
    {
        CanExecute = false,
        BlockReason = reason,
        CircuitBreakerState = state,
        EstimatedWaitTime = waitTime
    };
}

/// <summary>
/// Resilience metrics for monitoring
/// </summary>
public class ResilienceMetrics
{
    public Dictionary<string, OperationMetrics> OperationMetrics { get; init; } = new();
    public DateTime CollectionTime { get; init; } = DateTime.UtcNow;
    public TimeSpan CollectionPeriod { get; init; }
}

/// <summary>
/// Metrics for individual operations
/// </summary>
public class OperationMetrics
{
    public string OperationName { get; init; } = default!;
    public long TotalExecutions { get; init; }
    public long SuccessfulExecutions { get; init; }
    public long FailedExecutions { get; init; }
    public long RetriedExecutions { get; init; }
    public long CircuitBreakerOpenCount { get; init; }
    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions : 0;
    public TimeSpan AverageExecutionTime { get; init; }
    public TimeSpan MaxExecutionTime { get; init; }
    public CircuitBreakerState CurrentCircuitBreakerState { get; init; }
}