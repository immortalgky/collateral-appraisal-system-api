namespace Workflow.Workflow.Services;

/// <summary>
/// Resilience service for workflow operations with retry and fault handling
/// </summary>
public interface IWorkflowResilienceService
{
    Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
    Task ExecuteWithRetryAsync(Func<Task> operation, CancellationToken cancellationToken = default);
    Task<T> ExecuteWithRetryAsync<T>(Func<CancellationToken, Task<T>> operation, string policyName, CancellationToken cancellationToken = default);
    Task ExecuteWithRetryAsync(Func<CancellationToken, Task> operation, string policyName, CancellationToken cancellationToken = default);
    Task<T> ExecuteDatabaseOperationAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
    Task ExecuteDatabaseOperationAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
    Task<T> ExecuteExternalCallAsync<T>(Func<CancellationToken, Task<T>> operation, string serviceKey, CancellationToken cancellationToken = default);
    Task ExecuteExternalCallAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
    Task<T> ExecuteWorkflowActivityAsync<T>(Func<CancellationToken, Task<T>> operation, string activityType, CancellationToken cancellationToken = default);
    Task ExecuteWorkflowActivityAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}