using Assignment.Workflow.Activities.Core;

namespace Assignment.Workflow.Degradation;

/// <summary>
/// Service for handling graceful degradation when external services fail
/// Provides fallback mechanisms to keep workflow operations running even with partial system failures
/// </summary>
public interface IWorkflowDegradationService
{
    /// <summary>
    /// Executes an operation with automatic fallback to degraded functionality
    /// </summary>
    Task<T> ExecuteWithDegradationAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> primaryOperation,
        Func<Exception, CancellationToken, Task<T>>? fallbackOperation = null,
        DegradationPolicy? policy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a service is currently in degraded mode
    /// </summary>
    bool IsServiceDegraded(string serviceName);

    /// <summary>
    /// Gets the current degradation status for a service
    /// </summary>
    DegradationStatus GetDegradationStatus(string serviceName);

    /// <summary>
    /// Manually enables degraded mode for a service
    /// </summary>
    Task EnableDegradedModeAsync(
        string serviceName, 
        DegradationReason reason, 
        TimeSpan? duration = null,
        string? additionalInfo = null);

    /// <summary>
    /// Manually disables degraded mode for a service
    /// </summary>
    Task DisableDegradedModeAsync(string serviceName);

    /// <summary>
    /// Provides a fallback value when primary operation fails
    /// </summary>
    Task<T> ProvideFallbackAsync<T>(
        string operationName,
        Exception primaryException,
        FallbackStrategy strategy = FallbackStrategy.CachedValue,
        T? defaultValue = default);

    /// <summary>
    /// Records a successful operation to potentially restore service from degraded mode
    /// </summary>
    Task RecordSuccessfulOperationAsync(string serviceName);

    /// <summary>
    /// Gets degradation metrics for monitoring
    /// </summary>
    DegradationMetrics GetDegradationMetrics(string? serviceName = null);

    /// <summary>
    /// Validates that workflow can continue with current degradation status
    /// </summary>
    Task<WorkflowContinuationResult> ValidateWorkflowContinuationAsync(
        ActivityContext context,
        List<string> requiredServices,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Policy configuration for degradation handling
/// </summary>
public class DegradationPolicy
{
    /// <summary>
    /// Strategy to use when primary service fails
    /// </summary>
    public DegradationStrategy Strategy { get; init; } = DegradationStrategy.Fallback;

    /// <summary>
    /// Maximum time to wait before falling back
    /// </summary>
    public TimeSpan FallbackTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Whether to cache successful results for fallback use
    /// </summary>
    public bool EnableCaching { get; init; } = true;

    /// <summary>
    /// Cache expiration time for fallback data
    /// </summary>
    public TimeSpan CacheExpiry { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to allow partial functionality when service is degraded
    /// </summary>
    public bool AllowPartialFunctionality { get; init; } = true;

    /// <summary>
    /// Minimum service level required to continue operation
    /// </summary>
    public ServiceLevel MinimumServiceLevel { get; init; } = ServiceLevel.Degraded;

    /// <summary>
    /// Services that are critical for operation
    /// </summary>
    public List<string> CriticalServices { get; init; } = new();

    /// <summary>
    /// Services that are optional for operation
    /// </summary>
    public List<string> OptionalServices { get; init; } = new();

    /// <summary>
    /// Default degradation policy
    /// </summary>
    public static DegradationPolicy Default => new();

    /// <summary>
    /// Conservative policy that requires all services
    /// </summary>
    public static DegradationPolicy Conservative => new()
    {
        Strategy = DegradationStrategy.FailFast,
        AllowPartialFunctionality = false,
        MinimumServiceLevel = ServiceLevel.Full
    };

    /// <summary>
    /// Resilient policy that continues with minimal services
    /// </summary>
    public static DegradationPolicy Resilient => new()
    {
        Strategy = DegradationStrategy.Fallback,
        AllowPartialFunctionality = true,
        MinimumServiceLevel = ServiceLevel.Minimal,
        EnableCaching = true,
        CacheExpiry = TimeSpan.FromMinutes(15)
    };
}

/// <summary>
/// Strategies for handling service degradation
/// </summary>
public enum DegradationStrategy
{
    /// <summary>
    /// Fail immediately when service is unavailable
    /// </summary>
    FailFast,

    /// <summary>
    /// Use fallback mechanisms when service is unavailable
    /// </summary>
    Fallback,

    /// <summary>
    /// Continue with reduced functionality
    /// </summary>
    PartialFunctionality,

    /// <summary>
    /// Queue operations for later execution
    /// </summary>
    QueueForLater,

    /// <summary>
    /// Use cached data when service is unavailable
    /// </summary>
    CachedFallback
}

/// <summary>
/// Fallback strategies for providing alternative values
/// </summary>
public enum FallbackStrategy
{
    /// <summary>
    /// Use cached value from previous successful call
    /// </summary>
    CachedValue,

    /// <summary>
    /// Use a configured default value
    /// </summary>
    DefaultValue,

    /// <summary>
    /// Use an alternative service or data source
    /// </summary>
    AlternativeSource,

    /// <summary>
    /// Generate a computed fallback value
    /// </summary>
    ComputedValue,

    /// <summary>
    /// Return null/empty result
    /// </summary>
    EmptyResult
}

/// <summary>
/// Service levels for degradation control
/// </summary>
public enum ServiceLevel
{
    /// <summary>
    /// All services fully operational
    /// </summary>
    Full,

    /// <summary>
    /// Most services operational with some degradation
    /// </summary>
    Degraded,

    /// <summary>
    /// Core services operational, optional services may be unavailable
    /// </summary>
    Minimal,

    /// <summary>
    /// Critical services only, most functionality unavailable
    /// </summary>
    Critical,

    /// <summary>
    /// System unavailable
    /// </summary>
    Unavailable
}

/// <summary>
/// Reasons for service degradation
/// </summary>
public enum DegradationReason
{
    ServiceFailure,
    HighLatency,
    RateLimitExceeded,
    CircuitBreakerOpen,
    MaintenanceMode,
    ResourceExhaustion,
    NetworkIssues,
    ManualOverride,
    SecurityIncident,
    Unknown
}

/// <summary>
/// Current degradation status of a service
/// </summary>
public class DegradationStatus
{
    public string ServiceName { get; set; } = default!;
    public bool IsDegraded { get; set; }
    public ServiceLevel CurrentLevel { get; set; }
    public DegradationReason Reason { get; set; }
    public DateTime DegradedSince { get; set; }
    public TimeSpan? EstimatedRecoveryTime { get; set; }
    public string? AdditionalInfo { get; set; }
    public DateTime LastCheck { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int ConsecutiveSuccesses { get; set; }

    public static DegradationStatus Healthy(string serviceName) => new()
    {
        ServiceName = serviceName,
        IsDegraded = false,
        CurrentLevel = ServiceLevel.Full,
        LastCheck = DateTime.UtcNow
    };

    public static DegradationStatus Degraded(
        string serviceName, 
        DegradationReason reason, 
        ServiceLevel level = ServiceLevel.Degraded,
        string? additionalInfo = null) => new()
    {
        ServiceName = serviceName,
        IsDegraded = true,
        CurrentLevel = level,
        Reason = reason,
        DegradedSince = DateTime.UtcNow,
        AdditionalInfo = additionalInfo,
        LastCheck = DateTime.UtcNow
    };
}

/// <summary>
/// Result of workflow continuation validation
/// </summary>
public class WorkflowContinuationResult
{
    public bool CanContinue { get; set; }
    public ServiceLevel AvailableServiceLevel { get; set; }
    public List<string> UnavailableServices { get; set; } = new();
    public List<string> DegradedServices { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();

    public static WorkflowContinuationResult Allow(ServiceLevel serviceLevel) => new()
    {
        CanContinue = true,
        AvailableServiceLevel = serviceLevel
    };

    public static WorkflowContinuationResult Deny(string reason) => new()
    {
        CanContinue = false,
        AvailableServiceLevel = ServiceLevel.Unavailable,
        Warnings = new() { reason }
    };
}

/// <summary>
/// Metrics for degradation monitoring
/// </summary>
public class DegradationMetrics
{
    public Dictionary<string, ServiceDegradationMetrics> ServiceMetrics { get; set; } = new();
    public DateTime CollectionTime { get; set; } = DateTime.UtcNow;
    public ServiceLevel OverallServiceLevel { get; set; }
    public int TotalServices { get; set; }
    public int HealthyServices { get; set; }
    public int DegradedServices { get; set; }
    public int UnavailableServices { get; set; }
}

/// <summary>
/// Metrics for individual service degradation
/// </summary>
public class ServiceDegradationMetrics
{
    public string ServiceName { get; set; } = default!;
    public ServiceLevel CurrentLevel { get; set; }
    public bool IsDegraded { get; set; }
    public TimeSpan? DegradationDuration { get; set; }
    public long TotalOperations { get; set; }
    public long SuccessfulOperations { get; set; }
    public long FailedOperations { get; set; }
    public long FallbackOperations { get; set; }
    public double SuccessRate => TotalOperations > 0 ? (double)SuccessfulOperations / TotalOperations : 0;
    public double FallbackRate => TotalOperations > 0 ? (double)FallbackOperations / TotalOperations : 0;
    public TimeSpan AverageResponseTime { get; set; }
    public DegradationReason? LastDegradationReason { get; set; }
}