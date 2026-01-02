using Polly;
using Polly.Registry;

namespace Workflow.Workflow.Services;

/// <summary>
/// Production-ready resilience service using Microsoft.Extensions.Resilience
/// </summary>
public class WorkflowResilienceService : IWorkflowResilienceService
{
    private readonly ILogger<WorkflowResilienceService> _logger;
    private readonly ResiliencePipeline _retryPipeline;
    private readonly ResiliencePipeline _databasePipeline;
    private readonly ResiliencePipeline _externalPipeline;
    private readonly ResiliencePipeline _activityPipeline;

    public WorkflowResilienceService(
        ILogger<WorkflowResilienceService> logger,
        IServiceProvider serviceProvider,
        ResiliencePipelineProvider<string> resiliencePipelineProvider)
    {
        _logger = logger;
        _retryPipeline = resiliencePipelineProvider.GetPipeline("workflow-retry") ??
                         throw new InvalidOperationException("Resilience pipeline 'workflow-retry' not found");
        _databasePipeline = resiliencePipelineProvider.GetPipeline("workflow-database") ??
                            throw new InvalidOperationException("Resilience pipeline 'workflow-database' not found");
        _externalPipeline = resiliencePipelineProvider.GetPipeline("workflow-external") ??
                            throw new InvalidOperationException("Resilience pipeline 'workflow-external' not found");
        _activityPipeline = resiliencePipelineProvider.GetPipeline("workflow-activity") ??
                            throw new InvalidOperationException("Resilience pipeline 'workflow-activity' not found");
    }

    public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        return await _retryPipeline.ExecuteAsync(async token =>
        {
            var result = await operation();
            return result;
        }, cancellationToken);
    }

    public async Task ExecuteWithRetryAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        await _retryPipeline.ExecuteAsync(async token => { await operation(); }, cancellationToken);
    }

    public async Task<T> ExecuteWithRetryAsync<T>(Func<CancellationToken, Task<T>> operation, string policyName,
        CancellationToken cancellationToken = default)
    {
        var pipeline = GetPipelineByName(policyName);
        return await pipeline.ExecuteAsync(async token =>
        {
            var result = await operation(token);
            return result;
        }, cancellationToken);
    }

    public async Task ExecuteWithRetryAsync(Func<CancellationToken, Task> operation, string policyName,
        CancellationToken cancellationToken = default)
    {
        var pipeline = GetPipelineByName(policyName);
        await pipeline.ExecuteAsync(async token => { await operation(token); }, cancellationToken);
    }

    public async Task<T> ExecuteDatabaseOperationAsync<T>(Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        return await _databasePipeline.ExecuteAsync(async token =>
        {
            var result = await operation(token);
            return result;
        }, cancellationToken);
    }

    public async Task ExecuteDatabaseOperationAsync(Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await _databasePipeline.ExecuteAsync(async token => { await operation(token); }, cancellationToken);
    }

    public async Task<T> ExecuteExternalCallAsync<T>(Func<CancellationToken, Task<T>> operation, string serviceKey,
        CancellationToken cancellationToken = default)
    {
        return await _externalPipeline.ExecuteAsync(async token =>
        {
            _logger.LogDebug("Executing external call for service {ServiceKey}", serviceKey);
            var result = await operation(token);
            return result;
        }, cancellationToken);
    }

    public async Task ExecuteExternalCallAsync(Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await _externalPipeline.ExecuteAsync(async token => { await operation(token); }, cancellationToken);
    }

    public async Task<T> ExecuteWorkflowActivityAsync<T>(Func<CancellationToken, Task<T>> operation,
        string activityType, CancellationToken cancellationToken = default)
    {
        return await _activityPipeline.ExecuteAsync(async token =>
        {
            _logger.LogDebug("Executing workflow activity of type {ActivityType}", activityType);
            var result = await operation(token);
            return result;
        }, cancellationToken);
    }

    public async Task ExecuteWorkflowActivityAsync(Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await _activityPipeline.ExecuteAsync(async token => { await operation(token); }, cancellationToken);
    }

    private ResiliencePipeline GetPipelineByName(string policyName)
    {
        return policyName switch
        {
            "workflow-retry" => _retryPipeline,
            "workflow-database" => _databasePipeline,
            "workflow-external" => _externalPipeline,
            "workflow-activity" => _activityPipeline,
            _ => throw new InvalidOperationException($"Unknown resilience pipeline: {policyName}")
        };
    }
}