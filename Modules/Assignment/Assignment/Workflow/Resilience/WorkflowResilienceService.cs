using Assignment.Workflow.Services;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Assignment.Workflow.Resilience;

/// <summary>
/// Implementation of workflow resilience service with circuit breakers, retries, and timeout handling
/// Provides comprehensive fault tolerance and recovery mechanisms for workflow operations
/// </summary>
public class WorkflowResilienceService : IWorkflowResilienceService
{
    private readonly IWorkflowAuditService _auditService;
    private readonly ILogger<WorkflowResilienceService> _logger;
    private readonly ConcurrentDictionary<string, CircuitBreakerInstance> _circuitBreakers = new();
    private readonly ConcurrentDictionary<string, OperationMetrics> _operationMetrics = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _bulkheadSemaphores = new();
    private readonly Random _random = new();

    public WorkflowResilienceService(
        IWorkflowAuditService auditService,
        ILogger<WorkflowResilienceService> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<T> ExecuteWithResilienceAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> operation,
        ResiliencePolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        policy ??= ResiliencePolicy.Default;
        var operationId = Guid.NewGuid().ToString("N")[..8];
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("Starting resilient execution of operation '{OperationName}' with ID {OperationId}",
            operationName, operationId);

        try
        {
            // Validate operation can execute
            var validationResult = await ValidateOperationAsync(operationName, cancellationToken);
            if (!validationResult.CanExecute)
            {
                throw new OperationBlockedException(operationName, validationResult.BlockReason ?? "Unknown");
            }

            // Apply bulkhead isolation if configured
            using var bulkheadScope = await AcquireBulkheadAsync(operationName, policy.BulkheadPolicy, cancellationToken);

            // Execute with timeout and retry
            var result = await ExecuteWithRetryAsync(
                operationName,
                operation,
                policy.RetryPolicy,
                policy.TimeoutPolicy,
                operationId,
                cancellationToken);

            stopwatch.Stop();
            
            // Record success metrics
            await RecordOperationResultAsync(operationName, true, stopwatch.Elapsed, operationId);
            UpdateCircuitBreakerOnSuccess(operationName);

            _logger.LogDebug("Successfully completed resilient execution of operation '{OperationName}' with ID {OperationId} in {Duration}ms",
                operationName, operationId, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Record failure metrics
            await RecordOperationResultAsync(operationName, false, stopwatch.Elapsed, operationId, ex);
            UpdateCircuitBreakerOnFailure(operationName, ex);

            _logger.LogError(ex, "Failed resilient execution of operation '{OperationName}' with ID {OperationId} after {Duration}ms",
                operationName, operationId, stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    public async Task ExecuteWithResilienceAsync(
        string operationName,
        Func<CancellationToken, Task> operation,
        ResiliencePolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithResilienceAsync(
            operationName,
            async ct => 
            {
                await operation(ct);
                return true; // Dummy return value
            },
            policy,
            cancellationToken);
    }

    public async Task<T> ExecuteExternalServiceCallAsync<T>(
        string serviceName,
        Func<CancellationToken, Task<T>> serviceCall,
        Func<Exception, T>? fallbackProvider = null,
        ResiliencePolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        policy ??= ResiliencePolicy.Default;
        var operationName = $"ExternalService.{serviceName}";

        try
        {
            return await ExecuteWithResilienceAsync(operationName, serviceCall, policy, cancellationToken);
        }
        catch (Exception ex) when (policy.EnableFallback && fallbackProvider != null)
        {
            _logger.LogWarning(ex, "External service call to '{ServiceName}' failed, using fallback", serviceName);

            // Log fallback usage
            await _auditService.LogWorkflowEventAsync(
                Guid.Empty, // No specific workflow
                WorkflowAuditEventType.ConfigurationChanged,
                WorkflowAuditSeverity.Warning,
                $"Fallback activated for external service '{serviceName}': {ex.Message}",
                additionalData: new Dictionary<string, object>
                {
                    ["serviceName"] = serviceName,
                    ["exceptionType"] = ex.GetType().Name,
                    ["fallbackUsed"] = true
                },
                cancellationToken: cancellationToken);

            return fallbackProvider(ex);
        }
    }

    public CircuitBreakerState GetCircuitBreakerState(string operationName)
    {
        if (_circuitBreakers.TryGetValue(operationName, out var circuitBreaker))
        {
            return circuitBreaker.State;
        }
        return CircuitBreakerState.Closed;
    }

    public async Task OpenCircuitBreakerAsync(string operationName, TimeSpan? duration = null)
    {
        var circuitBreaker = GetOrCreateCircuitBreaker(operationName, CircuitBreakerPolicy.Default);
        circuitBreaker.Open(duration ?? TimeSpan.FromMinutes(5));

        _logger.LogWarning("Manually opened circuit breaker for operation '{OperationName}' for {Duration}",
            operationName, duration);

        await _auditService.LogWorkflowEventAsync(
            Guid.Empty,
            WorkflowAuditEventType.ConfigurationChanged,
            WorkflowAuditSeverity.Warning,
            $"Circuit breaker manually opened for operation '{operationName}'",
            additionalData: new Dictionary<string, object>
            {
                ["operationName"] = operationName,
                ["manualAction"] = true,
                ["duration"] = duration?.TotalSeconds ?? 300
            });
    }

    public async Task CloseCircuitBreakerAsync(string operationName)
    {
        if (_circuitBreakers.TryGetValue(operationName, out var circuitBreaker))
        {
            circuitBreaker.Close();
            
            _logger.LogInformation("Manually closed circuit breaker for operation '{OperationName}'", operationName);

            await _auditService.LogWorkflowEventAsync(
                Guid.Empty,
                WorkflowAuditEventType.ConfigurationChanged,
                WorkflowAuditSeverity.Information,
                $"Circuit breaker manually closed for operation '{operationName}'",
                additionalData: new Dictionary<string, object>
                {
                    ["operationName"] = operationName,
                    ["manualAction"] = true
                });
        }
    }

    public ResilienceMetrics GetResilienceMetrics(string? operationName = null)
    {
        var metrics = new ResilienceMetrics
        {
            CollectionTime = DateTime.UtcNow,
            CollectionPeriod = TimeSpan.FromMinutes(1)
        };

        if (operationName != null)
        {
            if (_operationMetrics.TryGetValue(operationName, out var operationMetric))
            {
                metrics.OperationMetrics[operationName] = operationMetric;
            }
        }
        else
        {
            foreach (var kvp in _operationMetrics)
            {
                metrics.OperationMetrics[kvp.Key] = kvp.Value;
            }
        }

        return metrics;
    }

    public Task<OperationValidationResult> ValidateOperationAsync(
        string operationName, 
        CancellationToken cancellationToken = default)
    {
        var circuitBreakerState = GetCircuitBreakerState(operationName);
        
        switch (circuitBreakerState)
        {
            case CircuitBreakerState.Open:
                var circuitBreaker = _circuitBreakers[operationName];
                var waitTime = circuitBreaker.OpenUntil - DateTime.UtcNow;
                return Task.FromResult(OperationValidationResult.Blocked(
                    "Circuit breaker is open", 
                    circuitBreakerState, 
                    waitTime > TimeSpan.Zero ? waitTime : null));

            case CircuitBreakerState.Disabled:
                return Task.FromResult(OperationValidationResult.Blocked(
                    "Circuit breaker is disabled", 
                    circuitBreakerState));

            default:
                return Task.FromResult(OperationValidationResult.Allowed);
        }
    }

    #region Private Implementation Methods

    private async Task<T> ExecuteWithRetryAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> operation,
        RetryPolicy retryPolicy,
        TimeoutPolicy timeoutPolicy,
        string operationId,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= retryPolicy.MaxRetryAttempts)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(timeoutPolicy.OperationTimeout);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var result = await operation(combinedCts.Token);
                
                if (attempt > 0)
                {
                    _logger.LogInformation("Operation '{OperationName}' succeeded on attempt {Attempt} (ID: {OperationId})",
                        operationName, attempt + 1, operationId);
                    
                    await RecordRetrySuccessAsync(operationName, attempt, operationId);
                }

                return result;
            }
            catch (Exception ex) when (attempt < retryPolicy.MaxRetryAttempts && ShouldRetry(ex, retryPolicy))
            {
                lastException = ex;
                attempt++;
                
                var delay = CalculateRetryDelay(retryPolicy, attempt);
                
                _logger.LogWarning(ex, "Operation '{OperationName}' failed on attempt {Attempt}, retrying in {Delay}ms (ID: {OperationId})",
                    operationName, attempt, delay.TotalMilliseconds, operationId);

                await RecordRetryAttemptAsync(operationName, attempt, ex, operationId);
                
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                // Either max retries reached or non-retriable exception
                if (attempt > 0)
                {
                    _logger.LogError(ex, "Operation '{OperationName}' failed after {Attempts} attempts (ID: {OperationId})",
                        operationName, attempt + 1, operationId);
                }
                throw;
            }
        }

        // This should never be reached, but just in case
        throw lastException ?? new InvalidOperationException("Retry logic failed");
    }

    private bool ShouldRetry(Exception exception, RetryPolicy retryPolicy)
    {
        // Check non-retriable exceptions first
        if (retryPolicy.NonRetriableExceptions.Any(type => type.IsInstanceOfType(exception)))
        {
            return false;
        }

        // Check retriable exceptions
        if (retryPolicy.RetriableExceptions.Any(type => type.IsInstanceOfType(exception)))
        {
            return true;
        }

        // Default behavior for common transient exceptions
        return exception is TimeoutException or 
               TaskCanceledException or 
               HttpRequestException ||
               (exception is InvalidOperationException ex && ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase));
    }

    private TimeSpan CalculateRetryDelay(RetryPolicy retryPolicy, int attempt)
    {
        var delay = retryPolicy.BackoffStrategy switch
        {
            BackoffStrategy.Fixed => retryPolicy.BaseDelay,
            BackoffStrategy.Linear => TimeSpan.FromTicks(retryPolicy.BaseDelay.Ticks * attempt),
            BackoffStrategy.Exponential => TimeSpan.FromTicks(retryPolicy.BaseDelay.Ticks * (long)Math.Pow(2, attempt - 1)),
            BackoffStrategy.ExponentialWithJitter => AddJitter(
                TimeSpan.FromTicks(retryPolicy.BaseDelay.Ticks * (long)Math.Pow(2, attempt - 1))),
            _ => retryPolicy.BaseDelay
        };

        return delay > retryPolicy.MaxDelay ? retryPolicy.MaxDelay : delay;
    }

    private TimeSpan AddJitter(TimeSpan delay)
    {
        var jitter = _random.NextDouble() * 0.1 * delay.TotalMilliseconds; // 10% jitter
        return delay.Add(TimeSpan.FromMilliseconds(jitter));
    }

    private async Task<IDisposable> AcquireBulkheadAsync(
        string operationName, 
        BulkheadPolicy? bulkheadPolicy, 
        CancellationToken cancellationToken)
    {
        if (bulkheadPolicy == null)
        {
            return new NullDisposable();
        }

        var semaphore = _bulkheadSemaphores.GetOrAdd(operationName, _ => new SemaphoreSlim(bulkheadPolicy.MaxConcurrency));

        if (!await semaphore.WaitAsync(bulkheadPolicy.QueueTimeout, cancellationToken))
        {
            throw new BulkheadRejectedException(operationName, "Bulkhead queue timeout exceeded");
        }

        return new SemaphoreReleaser(semaphore);
    }

    private CircuitBreakerInstance GetOrCreateCircuitBreaker(string operationName, CircuitBreakerPolicy policy)
    {
        return _circuitBreakers.GetOrAdd(operationName, _ => new CircuitBreakerInstance(policy));
    }

    private void UpdateCircuitBreakerOnSuccess(string operationName)
    {
        if (_circuitBreakers.TryGetValue(operationName, out var circuitBreaker))
        {
            circuitBreaker.RecordSuccess();
        }
    }

    private void UpdateCircuitBreakerOnFailure(string operationName, Exception exception)
    {
        var circuitBreaker = GetOrCreateCircuitBreaker(operationName, CircuitBreakerPolicy.Default);
        circuitBreaker.RecordFailure(exception);
    }

    private async Task RecordOperationResultAsync(
        string operationName, 
        bool success, 
        TimeSpan duration, 
        string operationId, 
        Exception? exception = null)
    {
        // Update metrics
        _operationMetrics.AddOrUpdate(operationName,
            new OperationMetrics
            {
                OperationName = operationName,
                TotalExecutions = 1,
                SuccessfulExecutions = success ? 1 : 0,
                FailedExecutions = success ? 0 : 1,
                AverageExecutionTime = duration,
                MaxExecutionTime = duration,
                CurrentCircuitBreakerState = GetCircuitBreakerState(operationName)
            },
            (key, existing) => new OperationMetrics
            {
                OperationName = operationName,
                TotalExecutions = existing.TotalExecutions + 1,
                SuccessfulExecutions = existing.SuccessfulExecutions + (success ? 1 : 0),
                FailedExecutions = existing.FailedExecutions + (success ? 0 : 1),
                RetriedExecutions = existing.RetriedExecutions,
                CircuitBreakerOpenCount = existing.CircuitBreakerOpenCount,
                AverageExecutionTime = TimeSpan.FromTicks((existing.AverageExecutionTime.Ticks + duration.Ticks) / 2),
                MaxExecutionTime = duration > existing.MaxExecutionTime ? duration : existing.MaxExecutionTime,
                CurrentCircuitBreakerState = GetCircuitBreakerState(operationName)
            });

        // Log to audit service for critical operations or failures
        if (!success || duration.TotalSeconds > 30)
        {
            await _auditService.LogPerformanceMetricsAsync(
                Guid.Empty,
                null,
                operationName,
                duration,
                success,
                new Dictionary<string, object>
                {
                    ["operationId"] = operationId,
                    ["circuitBreakerState"] = GetCircuitBreakerState(operationName).ToString(),
                    ["exceptionType"] = exception?.GetType().Name ?? ""
                });
        }
    }

    private async Task RecordRetryAttemptAsync(string operationName, int attempt, Exception exception, string operationId)
    {
        // Update retry metrics
        if (_operationMetrics.TryGetValue(operationName, out var metrics))
        {
            _operationMetrics[operationName] = new OperationMetrics
            {
                OperationName = metrics.OperationName,
                TotalExecutions = metrics.TotalExecutions,
                SuccessfulExecutions = metrics.SuccessfulExecutions,
                FailedExecutions = metrics.FailedExecutions,
                RetriedExecutions = metrics.RetriedExecutions + 1,
                CircuitBreakerOpenCount = metrics.CircuitBreakerOpenCount,
                AverageExecutionTime = metrics.AverageExecutionTime,
                MaxExecutionTime = metrics.MaxExecutionTime,
                CurrentCircuitBreakerState = metrics.CurrentCircuitBreakerState
            };
        }

        // Log retry attempt
        await _auditService.LogWorkflowEventAsync(
            Guid.Empty,
            WorkflowAuditEventType.StateTransition,
            WorkflowAuditSeverity.Warning,
            $"Retry attempt {attempt} for operation '{operationName}'",
            additionalData: new Dictionary<string, object>
            {
                ["operationName"] = operationName,
                ["operationId"] = operationId,
                ["attempt"] = attempt,
                ["exceptionType"] = exception.GetType().Name,
                ["exceptionMessage"] = exception.Message
            });
    }

    private async Task RecordRetrySuccessAsync(string operationName, int totalAttempts, string operationId)
    {
        await _auditService.LogWorkflowEventAsync(
            Guid.Empty,
            WorkflowAuditEventType.StateTransition,
            WorkflowAuditSeverity.Information,
            $"Operation '{operationName}' succeeded after {totalAttempts + 1} attempts",
            additionalData: new Dictionary<string, object>
            {
                ["operationName"] = operationName,
                ["operationId"] = operationId,
                ["totalAttempts"] = totalAttempts + 1,
                ["retrySuccess"] = true
            });
    }

    #endregion

    #region Helper Classes

    private class NullDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private class SemaphoreReleaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public SemaphoreReleaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            _semaphore.Release();
        }
    }

    #endregion
}

/// <summary>
/// Circuit breaker instance for tracking operation state
/// </summary>
internal class CircuitBreakerInstance
{
    private readonly CircuitBreakerPolicy _policy;
    private readonly object _lock = new();
    private readonly Queue<DateTime> _failureHistory = new();
    private readonly Queue<DateTime> _successHistory = new();
    
    public CircuitBreakerState State { get; private set; } = CircuitBreakerState.Closed;
    public DateTime OpenUntil { get; private set; }
    public int ConsecutiveFailures { get; private set; }

    public CircuitBreakerInstance(CircuitBreakerPolicy policy)
    {
        _policy = policy;
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            _successHistory.Enqueue(now);
            CleanupHistory(_successHistory, now);
            
            ConsecutiveFailures = 0;

            if (State == CircuitBreakerState.HalfOpen)
            {
                var successRate = CalculateSuccessRate();
                if (successRate >= _policy.SuccessThreshold)
                {
                    State = CircuitBreakerState.Closed;
                }
            }
        }
    }

    public void RecordFailure(Exception exception)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            _failureHistory.Enqueue(now);
            CleanupHistory(_failureHistory, now);
            
            ConsecutiveFailures++;

            if (State == CircuitBreakerState.Closed && ShouldOpen())
            {
                State = CircuitBreakerState.Open;
                OpenUntil = now.Add(_policy.OpenCircuitDuration);
            }
            else if (State == CircuitBreakerState.HalfOpen)
            {
                State = CircuitBreakerState.Open;
                OpenUntil = now.Add(_policy.OpenCircuitDuration);
            }
        }
    }

    public void Open(TimeSpan duration)
    {
        lock (_lock)
        {
            State = CircuitBreakerState.Open;
            OpenUntil = DateTime.UtcNow.Add(duration);
        }
    }

    public void Close()
    {
        lock (_lock)
        {
            State = CircuitBreakerState.Closed;
            ConsecutiveFailures = 0;
        }
    }

    private bool ShouldOpen()
    {
        var now = DateTime.UtcNow;
        var totalRequests = _failureHistory.Count + _successHistory.Count;
        
        return totalRequests >= _policy.MinimumThroughput &&
               _failureHistory.Count >= _policy.FailureThreshold;
    }

    private double CalculateSuccessRate()
    {
        var totalRequests = _failureHistory.Count + _successHistory.Count;
        return totalRequests > 0 ? (double)_successHistory.Count / totalRequests : 1.0;
    }

    private void CleanupHistory(Queue<DateTime> history, DateTime now)
    {
        while (history.Count > 0 && now - history.Peek() > _policy.SamplingWindow)
        {
            history.Dequeue();
        }
    }
}

/// <summary>
/// Exception thrown when an operation is blocked by resilience policies
/// </summary>
public class OperationBlockedException : Exception
{
    public string OperationName { get; }

    public OperationBlockedException(string operationName, string reason) 
        : base($"Operation '{operationName}' was blocked: {reason}")
    {
        OperationName = operationName;
    }
}

/// <summary>
/// Exception thrown when bulkhead capacity is exceeded
/// </summary>
public class BulkheadRejectedException : Exception
{
    public string OperationName { get; }

    public BulkheadRejectedException(string operationName, string reason)
        : base($"Operation '{operationName}' was rejected by bulkhead: {reason}")
    {
        OperationName = operationName;
    }
}