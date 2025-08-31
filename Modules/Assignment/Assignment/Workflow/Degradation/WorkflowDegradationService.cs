using Assignment.Workflow.Services;
using System.Collections.Concurrent;
using System.Diagnostics;
using ActivityContext = Assignment.Workflow.Activities.Core.ActivityContext;

namespace Assignment.Workflow.Degradation;

/// <summary>
/// Implementation of workflow degradation service for handling external service failures gracefully
/// </summary>
public class WorkflowDegradationService : IWorkflowDegradationService
{
    private readonly IWorkflowAuditService _auditService;
    private readonly ILogger<WorkflowDegradationService> _logger;
    private readonly ConcurrentDictionary<string, DegradationStatus> _serviceStatuses = new();
    private readonly ConcurrentDictionary<string, ServiceDegradationMetrics> _serviceMetrics = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<CachedResult>> _resultCache = new();
    private readonly Timer _statusCheckTimer;

    public WorkflowDegradationService(
        IWorkflowAuditService auditService,
        ILogger<WorkflowDegradationService> logger)
    {
        _auditService = auditService;
        _logger = logger;

        // Start periodic status check timer (every 30 seconds)
        _statusCheckTimer = new Timer(PeriodicStatusCheck, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task<T> ExecuteWithDegradationAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> primaryOperation,
        Func<Exception, CancellationToken, Task<T>>? fallbackOperation = null,
        DegradationPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        policy ??= DegradationPolicy.Default;
        var serviceName = ExtractServiceName(operationName);
        var stopwatch = Stopwatch.StartNew();
        var operationId = Guid.NewGuid().ToString("N")[..8];

        _logger.LogDebug("Executing operation '{OperationName}' with degradation handling (ID: {OperationId})",
            operationName, operationId);

        // Check if service is currently degraded
        var status = GetDegradationStatus(serviceName);
        if (status.IsDegraded && policy.Strategy == DegradationStrategy.FailFast)
        {
            throw new ServiceDegradedException(serviceName, status.Reason, "Service is degraded and fail-fast policy is active");
        }

        try
        {
            // Try primary operation first
            var result = await ExecutePrimaryOperationAsync(primaryOperation, policy.FallbackTimeout, cancellationToken);
            stopwatch.Stop();

            // Record successful operation
            await RecordOperationResultAsync(serviceName, true, stopwatch.Elapsed, operationId);
            await RecordSuccessfulOperationAsync(serviceName);

            // Cache successful result if caching is enabled
            if (policy.EnableCaching)
            {
                CacheResult(operationName, result, policy.CacheExpiry);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await RecordOperationResultAsync(serviceName, false, stopwatch.Elapsed, operationId, ex);
            
            _logger.LogWarning(ex, "Primary operation '{OperationName}' failed, attempting degradation handling (ID: {OperationId})",
                operationName, operationId);

            // Update service status based on failure
            await UpdateServiceStatusOnFailure(serviceName, ex);

            // Handle degradation based on strategy
            return await HandleDegradationAsync(
                operationName,
                ex,
                fallbackOperation,
                policy,
                operationId,
                cancellationToken);
        }
    }

    public bool IsServiceDegraded(string serviceName)
    {
        return _serviceStatuses.TryGetValue(serviceName, out var status) && status.IsDegraded;
    }

    public DegradationStatus GetDegradationStatus(string serviceName)
    {
        return _serviceStatuses.GetOrAdd(serviceName, _ => DegradationStatus.Healthy(serviceName));
    }

    public async Task EnableDegradedModeAsync(
        string serviceName, 
        DegradationReason reason, 
        TimeSpan? duration = null,
        string? additionalInfo = null)
    {
        var status = DegradationStatus.Degraded(serviceName, reason, ServiceLevel.Degraded, additionalInfo);
        _serviceStatuses.AddOrUpdate(serviceName, status, (_, _) => status);

        _logger.LogWarning("Manually enabled degraded mode for service '{ServiceName}': {Reason}. Duration: {Duration}",
            serviceName, reason, duration?.TotalMinutes.ToString("F1") + "min" ?? "indefinite");

        await _auditService.LogWorkflowEventAsync(
            Guid.Empty,
            WorkflowAuditEventType.ConfigurationChanged,
            WorkflowAuditSeverity.Warning,
            $"Service '{serviceName}' manually set to degraded mode",
            additionalData: new Dictionary<string, object>
            {
                ["serviceName"] = serviceName,
                ["reason"] = reason.ToString(),
                ["duration"] = duration?.TotalSeconds ?? -1,
                ["additionalInfo"] = additionalInfo ?? "",
                ["manualAction"] = true
            });

        // Schedule automatic recovery if duration is specified
        if (duration.HasValue)
        {
            _ = Task.Delay(duration.Value).ContinueWith(async _ =>
            {
                await DisableDegradedModeAsync(serviceName);
            });
        }
    }

    public async Task DisableDegradedModeAsync(string serviceName)
    {
        var healthyStatus = DegradationStatus.Healthy(serviceName);
        _serviceStatuses.AddOrUpdate(serviceName, healthyStatus, (_, _) => healthyStatus);

        _logger.LogInformation("Disabled degraded mode for service '{ServiceName}' - service restored to healthy status",
            serviceName);

        await _auditService.LogWorkflowEventAsync(
            Guid.Empty,
            WorkflowAuditEventType.ConfigurationChanged,
            WorkflowAuditSeverity.Information,
            $"Service '{serviceName}' restored from degraded mode",
            additionalData: new Dictionary<string, object>
            {
                ["serviceName"] = serviceName,
                ["restored"] = true
            });
    }

    public Task<T> ProvideFallbackAsync<T>(
        string operationName,
        Exception primaryException,
        FallbackStrategy strategy = FallbackStrategy.CachedValue,
        T? defaultValue = default)
    {
        return strategy switch
        {
            FallbackStrategy.CachedValue => ProvideCachedFallbackAsync<T>(operationName),
            FallbackStrategy.DefaultValue => Task.FromResult(defaultValue ?? default(T)!),
            FallbackStrategy.EmptyResult => Task.FromResult(CreateEmptyResult<T>()),
            FallbackStrategy.ComputedValue => ProvideComputedFallbackAsync<T>(operationName, primaryException),
            FallbackStrategy.AlternativeSource => ProvideAlternativeFallbackAsync<T>(operationName, primaryException),
            _ => Task.FromResult(defaultValue ?? default(T)!)
        };
    }

    public async Task RecordSuccessfulOperationAsync(string serviceName)
    {
        var currentStatus = GetDegradationStatus(serviceName);
        
        if (currentStatus.IsDegraded)
        {
            var newSuccessCount = currentStatus.ConsecutiveSuccesses + 1;
            
            // Consider recovery after 3 consecutive successes
            if (newSuccessCount >= 3)
            {
                await DisableDegradedModeAsync(serviceName);
                
                _logger.LogInformation("Service '{ServiceName}' automatically recovered after {SuccessCount} consecutive successful operations",
                    serviceName, newSuccessCount);
            }
            else
            {
                // Update success count
                var updatedStatus = new DegradationStatus
                {
                    ServiceName = currentStatus.ServiceName,
                    IsDegraded = currentStatus.IsDegraded,
                    CurrentLevel = currentStatus.CurrentLevel,
                    Reason = currentStatus.Reason,
                    DegradedSince = currentStatus.DegradedSince,
                    EstimatedRecoveryTime = currentStatus.EstimatedRecoveryTime,
                    AdditionalInfo = currentStatus.AdditionalInfo,
                    ConsecutiveSuccesses = newSuccessCount,
                    ConsecutiveFailures = 0,
                    LastCheck = DateTime.UtcNow
                };
                _serviceStatuses.TryUpdate(serviceName, updatedStatus, currentStatus);
            }
        }

        // Update metrics
        UpdateServiceMetrics(serviceName, true, TimeSpan.Zero);
    }

    public DegradationMetrics GetDegradationMetrics(string? serviceName = null)
    {
        var metrics = new DegradationMetrics();

        if (serviceName != null)
        {
            if (_serviceMetrics.TryGetValue(serviceName, out var serviceMetric))
            {
                metrics.ServiceMetrics[serviceName] = serviceMetric;
            }
        }
        else
        {
            foreach (var kvp in _serviceMetrics)
            {
                metrics.ServiceMetrics[kvp.Key] = kvp.Value;
            }
        }

        // Calculate overall statistics
        metrics.TotalServices = _serviceStatuses.Count;
        metrics.HealthyServices = _serviceStatuses.Count(s => !s.Value.IsDegraded);
        metrics.DegradedServices = _serviceStatuses.Count(s => s.Value.IsDegraded && s.Value.CurrentLevel != ServiceLevel.Unavailable);
        metrics.UnavailableServices = _serviceStatuses.Count(s => s.Value.CurrentLevel == ServiceLevel.Unavailable);

        // Determine overall service level
        metrics.OverallServiceLevel = DetermineOverallServiceLevel();

        return metrics;
    }

    public async Task<WorkflowContinuationResult> ValidateWorkflowContinuationAsync(
        ActivityContext context,
        List<string> requiredServices,
        CancellationToken cancellationToken = default)
    {
        var result = new WorkflowContinuationResult();
        var warnings = new List<string>();
        var recommendations = new List<string>();

        foreach (var serviceName in requiredServices)
        {
            var status = GetDegradationStatus(serviceName);
            
            if (status.IsDegraded)
            {
                if (status.CurrentLevel == ServiceLevel.Unavailable)
                {
                    result.UnavailableServices.Add(serviceName);
                    warnings.Add($"Critical service '{serviceName}' is unavailable");
                }
                else
                {
                    result.DegradedServices.Add(serviceName);
                    warnings.Add($"Service '{serviceName}' is running in degraded mode");
                    recommendations.Add($"Monitor service '{serviceName}' for recovery");
                }
            }
        }

        // Determine if workflow can continue
        result.CanContinue = result.UnavailableServices.Count == 0;
        result.AvailableServiceLevel = DetermineAvailableServiceLevel(requiredServices);
        result.Warnings = warnings;
        result.Recommendations = recommendations;

        // Log validation result if there are issues
        if (!result.CanContinue || result.DegradedServices.Any())
        {
            await _auditService.LogActivityEventAsync(
                context,
                ActivityAuditEventType.ValidationFailed,
                result.CanContinue ? WorkflowAuditSeverity.Warning : WorkflowAuditSeverity.Error,
                $"Workflow continuation validation: {(result.CanContinue ? "Can continue with degradation" : "Blocked by service failures")}",
                context.CurrentAssignee,
                new Dictionary<string, object>
                {
                    ["requiredServices"] = requiredServices,
                    ["unavailableServices"] = result.UnavailableServices,
                    ["degradedServices"] = result.DegradedServices,
                    ["canContinue"] = result.CanContinue,
                    ["serviceLevel"] = result.AvailableServiceLevel.ToString()
                },
                cancellationToken);
        }

        return result;
    }

    #region Private Implementation Methods

    private async Task<T> ExecutePrimaryOperationAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        return await operation(combinedCts.Token);
    }

    private async Task<T> HandleDegradationAsync<T>(
        string operationName,
        Exception primaryException,
        Func<Exception, CancellationToken, Task<T>>? fallbackOperation,
        DegradationPolicy policy,
        string operationId,
        CancellationToken cancellationToken)
    {
        var serviceName = ExtractServiceName(operationName);

        switch (policy.Strategy)
        {
            case DegradationStrategy.FailFast:
                throw new ServiceDegradedException(serviceName, DegradationReason.ServiceFailure, primaryException.Message, primaryException);

            case DegradationStrategy.Fallback:
                if (fallbackOperation != null)
                {
                    try
                    {
                        _logger.LogInformation("Executing fallback operation for '{OperationName}' (ID: {OperationId})",
                            operationName, operationId);

                        var fallbackResult = await fallbackOperation(primaryException, cancellationToken);
                        await RecordFallbackUsageAsync(serviceName, operationId);
                        return fallbackResult;
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogError(fallbackEx, "Fallback operation also failed for '{OperationName}' (ID: {OperationId})",
                            operationName, operationId);
                        throw new AggregateException("Both primary and fallback operations failed", primaryException, fallbackEx);
                    }
                }
                goto case DegradationStrategy.CachedFallback;

            case DegradationStrategy.CachedFallback:
                try
                {
                    var cachedResult = await ProvideCachedFallbackAsync<T>(operationName);
                    await RecordFallbackUsageAsync(serviceName, operationId);
                    return cachedResult;
                }
                catch
                {
                    goto case DegradationStrategy.PartialFunctionality;
                }

            case DegradationStrategy.PartialFunctionality:
                if (policy.AllowPartialFunctionality)
                {
                    var partialResult = CreateEmptyResult<T>();
                    await RecordFallbackUsageAsync(serviceName, operationId);
                    return partialResult;
                }
                throw new ServiceDegradedException(serviceName, DegradationReason.ServiceFailure, primaryException.Message, primaryException);

            case DegradationStrategy.QueueForLater:
                throw new NotImplementedException("Queue for later strategy is not yet implemented");

            default:
                throw new ServiceDegradedException(serviceName, DegradationReason.ServiceFailure, primaryException.Message, primaryException);
        }
    }

    private async Task UpdateServiceStatusOnFailure(string serviceName, Exception exception)
    {
        var currentStatus = GetDegradationStatus(serviceName);
        var newFailureCount = currentStatus.ConsecutiveFailures + 1;
        var reason = DetermineFailureReason(exception);

        // Consider service degraded after 3 consecutive failures
        if (newFailureCount >= 3 && !currentStatus.IsDegraded)
        {
            var degradedStatus = DegradationStatus.Degraded(serviceName, reason, ServiceLevel.Degraded);
            _serviceStatuses.TryUpdate(serviceName, degradedStatus, currentStatus);

            _logger.LogWarning("Service '{ServiceName}' automatically marked as degraded after {FailureCount} consecutive failures",
                serviceName, newFailureCount);

            await _auditService.LogWorkflowEventAsync(
                Guid.Empty,
                WorkflowAuditEventType.StateTransition,
                WorkflowAuditSeverity.Warning,
                $"Service '{serviceName}' automatically degraded due to consecutive failures",
                additionalData: new Dictionary<string, object>
                {
                    ["serviceName"] = serviceName,
                    ["consecutiveFailures"] = newFailureCount,
                    ["reason"] = reason.ToString(),
                    ["automatic"] = true
                });
        }
        else
        {
            // Update failure count
            var updatedStatus = new DegradationStatus
            {
                ServiceName = currentStatus.ServiceName,
                IsDegraded = currentStatus.IsDegraded,
                CurrentLevel = currentStatus.CurrentLevel,
                Reason = currentStatus.Reason,
                DegradedSince = currentStatus.DegradedSince,
                EstimatedRecoveryTime = currentStatus.EstimatedRecoveryTime,
                AdditionalInfo = currentStatus.AdditionalInfo,
                ConsecutiveFailures = newFailureCount,
                ConsecutiveSuccesses = 0,
                LastCheck = DateTime.UtcNow
            };
            _serviceStatuses.TryUpdate(serviceName, updatedStatus, currentStatus);
        }
    }

    private Task<T> ProvideCachedFallbackAsync<T>(string operationName)
    {
        if (_resultCache.TryGetValue(operationName, out var cache) && cache.TryDequeue(out var cachedResult))
        {
            if (cachedResult.ExpiresAt > DateTime.UtcNow && cachedResult.Result is T result)
            {
                _logger.LogInformation("Using cached fallback result for operation '{OperationName}'", operationName);
                return Task.FromResult(result);
            }
        }

        throw new InvalidOperationException($"No valid cached result available for operation '{operationName}'");
    }

    private Task<T> ProvideComputedFallbackAsync<T>(string operationName, Exception primaryException)
    {
        // This could be enhanced to provide intelligent computed fallbacks based on operation type
        _logger.LogInformation("Providing computed fallback for operation '{OperationName}'", operationName);
        return Task.FromResult(CreateEmptyResult<T>());
    }

    private Task<T> ProvideAlternativeFallbackAsync<T>(string operationName, Exception primaryException)
    {
        // This could be enhanced to use alternative data sources or services
        _logger.LogInformation("Providing alternative source fallback for operation '{OperationName}'", operationName);
        return Task.FromResult(CreateEmptyResult<T>());
    }

    private T CreateEmptyResult<T>()
    {
        if (typeof(T) == typeof(string))
            return (T)(object)"";
        if (typeof(T) == typeof(bool))
            return (T)(object)false;
        if (typeof(T).IsValueType)
            return default(T)!;
        
        return Activator.CreateInstance<T>();
    }

    private void CacheResult<T>(string operationName, T result, TimeSpan expiry)
    {
        var cache = _resultCache.GetOrAdd(operationName, _ => new ConcurrentQueue<CachedResult>());
        var cachedResult = new CachedResult
        {
            Result = result,
            CachedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(expiry)
        };
        
        cache.Enqueue(cachedResult);

        // Keep only the last 5 cached results per operation
        while (cache.Count > 5)
        {
            cache.TryDequeue(out _);
        }
    }

    private string ExtractServiceName(string operationName)
    {
        // Extract service name from operation name (e.g., "Webhook.api.example.com" -> "api.example.com")
        var parts = operationName.Split('.', 2);
        return parts.Length > 1 ? parts[1] : operationName;
    }

    private DegradationReason DetermineFailureReason(Exception exception)
    {
        return exception switch
        {
            TimeoutException => DegradationReason.HighLatency,
            HttpRequestException httpEx when httpEx.Message.Contains("429") => DegradationReason.RateLimitExceeded,
            HttpRequestException httpEx when httpEx.Message.Contains("503") => DegradationReason.ServiceFailure,
            TaskCanceledException => DegradationReason.HighLatency,
            _ => DegradationReason.ServiceFailure
        };
    }

    private ServiceLevel DetermineOverallServiceLevel()
    {
        if (!_serviceStatuses.Any())
            return ServiceLevel.Full;

        var totalServices = _serviceStatuses.Count;
        var healthyServices = _serviceStatuses.Count(s => !s.Value.IsDegraded);
        var unavailableServices = _serviceStatuses.Count(s => s.Value.CurrentLevel == ServiceLevel.Unavailable);

        var healthyPercentage = (double)healthyServices / totalServices;

        return healthyPercentage switch
        {
            >= 0.9 => ServiceLevel.Full,
            >= 0.7 => ServiceLevel.Degraded,
            >= 0.5 => ServiceLevel.Minimal,
            > 0.0 => ServiceLevel.Critical,
            _ => ServiceLevel.Unavailable
        };
    }

    private ServiceLevel DetermineAvailableServiceLevel(List<string> requiredServices)
    {
        if (!requiredServices.Any())
            return ServiceLevel.Full;

        var serviceStatuses = requiredServices.Select(GetDegradationStatus).ToList();
        var healthyCount = serviceStatuses.Count(s => !s.IsDegraded);
        var healthyPercentage = (double)healthyCount / serviceStatuses.Count;

        return healthyPercentage switch
        {
            >= 0.9 => ServiceLevel.Full,
            >= 0.7 => ServiceLevel.Degraded,
            >= 0.5 => ServiceLevel.Minimal,
            > 0.0 => ServiceLevel.Critical,
            _ => ServiceLevel.Unavailable
        };
    }

    private async Task RecordOperationResultAsync(
        string serviceName,
        bool success,
        TimeSpan duration,
        string operationId,
        Exception? exception = null)
    {
        UpdateServiceMetrics(serviceName, success, duration);

        // Log significant events to audit service
        if (!success || duration.TotalSeconds > 10)
        {
            await _auditService.LogPerformanceMetricsAsync(
                Guid.Empty,
                null,
                $"Service.{serviceName}",
                duration,
                success,
                new Dictionary<string, object>
                {
                    ["operationId"] = operationId,
                    ["serviceName"] = serviceName,
                    ["exceptionType"] = exception?.GetType().Name ?? ""
                });
        }
    }

    private async Task RecordFallbackUsageAsync(string serviceName, string operationId)
    {
        UpdateServiceMetrics(serviceName, success: false, TimeSpan.Zero, fallbackUsed: true);

        await _auditService.LogWorkflowEventAsync(
            Guid.Empty,
            WorkflowAuditEventType.StateTransition,
            WorkflowAuditSeverity.Information,
            $"Fallback mechanism used for service '{serviceName}'",
            additionalData: new Dictionary<string, object>
            {
                ["serviceName"] = serviceName,
                ["operationId"] = operationId,
                ["fallbackUsed"] = true
            });
    }

    private void UpdateServiceMetrics(string serviceName, bool success, TimeSpan duration, bool fallbackUsed = false)
    {
        _serviceMetrics.AddOrUpdate(serviceName,
            new ServiceDegradationMetrics
            {
                ServiceName = serviceName,
                CurrentLevel = GetDegradationStatus(serviceName).CurrentLevel,
                IsDegraded = GetDegradationStatus(serviceName).IsDegraded,
                TotalOperations = 1,
                SuccessfulOperations = success ? 1 : 0,
                FailedOperations = success ? 0 : 1,
                FallbackOperations = fallbackUsed ? 1 : 0,
                AverageResponseTime = duration
            },
            (key, existing) =>
            {
                var status = GetDegradationStatus(serviceName);
                return new ServiceDegradationMetrics
                {
                    ServiceName = existing.ServiceName,
                    CurrentLevel = status.CurrentLevel,
                    IsDegraded = status.IsDegraded,
                    DegradationDuration = status.IsDegraded ? DateTime.UtcNow - status.DegradedSince : null,
                    TotalOperations = existing.TotalOperations + 1,
                    SuccessfulOperations = existing.SuccessfulOperations + (success ? 1 : 0),
                    FailedOperations = existing.FailedOperations + (success ? 0 : 1),
                    FallbackOperations = existing.FallbackOperations + (fallbackUsed ? 1 : 0),
                    AverageResponseTime = TimeSpan.FromTicks((existing.AverageResponseTime.Ticks + duration.Ticks) / 2),
                    LastDegradationReason = status.IsDegraded ? status.Reason : existing.LastDegradationReason
                };
            });
    }

    private void PeriodicStatusCheck(object? state)
    {
        try
        {
            // Clean up expired cache entries
            foreach (var cache in _resultCache.Values)
            {
                var now = DateTime.UtcNow;
                var itemsToKeep = new List<CachedResult>();

                while (cache.TryDequeue(out var item))
                {
                    if (item.ExpiresAt > now)
                    {
                        itemsToKeep.Add(item);
                    }
                }

                // Re-enqueue non-expired items
                foreach (var item in itemsToKeep)
                {
                    cache.Enqueue(item);
                }
            }

            _logger.LogDebug("Completed periodic degradation status check");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic degradation status check");
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _statusCheckTimer?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Helper Classes

    private class CachedResult
    {
        public object? Result { get; init; }
        public DateTime CachedAt { get; init; }
        public DateTime ExpiresAt { get; init; }
    }

    #endregion
}

/// <summary>
/// Exception thrown when a service is degraded and cannot fulfill the request
/// </summary>
public class ServiceDegradedException : Exception
{
    public string ServiceName { get; }
    public DegradationReason Reason { get; }

    public ServiceDegradedException(string serviceName, DegradationReason reason, string message) 
        : base($"Service '{serviceName}' is degraded ({reason}): {message}")
    {
        ServiceName = serviceName;
        Reason = reason;
    }

    public ServiceDegradedException(string serviceName, DegradationReason reason, string message, Exception innerException)
        : base($"Service '{serviceName}' is degraded ({reason}): {message}", innerException)
    {
        ServiceName = serviceName;
        Reason = reason;
    }
}