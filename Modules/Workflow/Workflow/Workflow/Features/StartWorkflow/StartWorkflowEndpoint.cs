using Workflow.Workflow.Services;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Engine;
using Workflow.Workflow.Engine.Core;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Features.StartWorkflow;

public class StartWorkflowEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workflows/instances/start", async (
                StartWorkflowRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new StartWorkflowCommand
                {
                    WorkflowDefinitionId = request.WorkflowDefinitionId,
                    InstanceName = request.InstanceName,
                    StartedBy = request.StartedBy,
                    InitialVariables = request.InitialVariables,
                    CorrelationId = request.CorrelationId,
                    AssignmentOverrides = request.AssignmentOverrides
                };

                var result = await sender.Send(command, cancellationToken);

                return Results.Ok(result);
            })
            .WithName("StartWorkflow")
            .WithTags("Workflows");
    }
}

public record StartWorkflowRequest
{
    public Guid WorkflowDefinitionId { get; init; }
    public string InstanceName { get; init; } = default!;
    public string StartedBy { get; init; } = default!;
    public Dictionary<string, object>? InitialVariables { get; init; }
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Assignment overrides for specific activities in the workflow
    /// Key = ActivityId, Value = Assignment override details
    /// </summary>
    public Dictionary<string, AssignmentOverrideRequest>? AssignmentOverrides { get; init; }
}

/// <summary>
/// Request model for assignment overrides
/// </summary>
public record AssignmentOverrideRequest
{
    /// <summary>
    /// Specific user to assign the task to
    /// </summary>
    public string? RuntimeAssignee { get; init; }

    /// <summary>
    /// Specific group to assign the task to
    /// </summary>
    public string? RuntimeAssigneeGroup { get; init; }

    /// <summary>
    /// Custom assignment strategies to use
    /// </summary>
    public List<string>? RuntimeAssignmentStrategies { get; init; }

    /// <summary>
    /// Reason for the override
    /// </summary>
    public string? OverrideReason { get; init; }

    /// <summary>
    /// Additional properties to override
    /// </summary>
    public Dictionary<string, object>? OverrideProperties { get; init; }
}

public record StartWorkflowCommand : ICommand<StartWorkflowResponse>
{
    public Guid WorkflowDefinitionId { get; init; }
    public string InstanceName { get; init; } = default!;
    public string StartedBy { get; init; } = default!;
    public Dictionary<string, object>? InitialVariables { get; init; }
    public string? CorrelationId { get; init; }
    public Dictionary<string, AssignmentOverrideRequest>? AssignmentOverrides { get; init; }
}

public record StartWorkflowResponse
{
    public Guid WorkflowInstanceId { get; init; }
    public string InstanceName { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string NextActivityId { get; init; } = default!;
    public string? NextAssignee { get; init; }
    public DateTime StartedOn { get; init; }
}

public class StartWorkflowCommandHandler : ICommandHandler<StartWorkflowCommand, StartWorkflowResponse>
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowOrchestrator _orchestrator;
    private readonly IWorkflowPersistenceService _persistenceService;
    private readonly IWorkflowEventPublisher _eventPublisher;
    private readonly IWorkflowFaultHandler _faultHandler;
    private readonly ILogger<StartWorkflowCommandHandler> _logger;

    public StartWorkflowCommandHandler(
        IWorkflowEngine workflowEngine,
        IWorkflowOrchestrator orchestrator,
        IWorkflowPersistenceService persistenceService,
        IWorkflowEventPublisher eventPublisher,
        IWorkflowFaultHandler faultHandler,
        ILogger<StartWorkflowCommandHandler> logger)
    {
        _workflowEngine = workflowEngine;
        _orchestrator = orchestrator;
        _persistenceService = persistenceService;
        _eventPublisher = eventPublisher;
        _faultHandler = faultHandler;
        _logger = logger;
    }

    public async Task<StartWorkflowResponse> Handle(StartWorkflowCommand request, CancellationToken cancellationToken)
    {
        int attemptNumber = 1;
        const int maxAttempts = 3;

        while (attemptNumber <= maxAttempts)
        {
            try
            {
                _logger.LogInformation("HANDLER: Starting workflow for definition {WorkflowDefinitionId} (attempt {AttemptNumber})",
                    request.WorkflowDefinitionId, attemptNumber);

                // Convert assignment override requests to runtime override objects
                Dictionary<string, RuntimeOverride>? runtimeOverrides = null;
                if (request.AssignmentOverrides != null)
                {
                    runtimeOverrides = new Dictionary<string, RuntimeOverride>();
                    foreach (var kvp in request.AssignmentOverrides)
                    {
                        var activityId = kvp.Key;
                        var overrideRequest = kvp.Value;

                        runtimeOverrides[activityId] = new RuntimeOverride
                        {
                            RuntimeAssignee = overrideRequest.RuntimeAssignee,
                            RuntimeAssigneeGroup = overrideRequest.RuntimeAssigneeGroup,
                            RuntimeAssignmentStrategies = overrideRequest.RuntimeAssignmentStrategies,
                            OverrideReason = overrideRequest.OverrideReason,
                            OverrideProperties = overrideRequest.OverrideProperties,
                            OverrideBy = request.StartedBy,
                            CreatedAt = DateTime.UtcNow
                        };
                    }
                }

                // ENHANCED: Execute startup with atomic transaction boundaries
                var workflowInstance = await _persistenceService.ExecuteInTransactionAsync(async () =>
                {
                    // 1. Start workflow with single-step execution (atomic)
                    var startupResult = await _workflowEngine.StartWorkflowAsync(
                        request.WorkflowDefinitionId, 
                        request.InstanceName, 
                        request.StartedBy, 
                        request.InitialVariables, 
                        request.CorrelationId, 
                        runtimeOverrides, 
                        cancellationToken);

                    if (startupResult.Status == WorkflowExecutionStatus.Failed)
                        throw new InvalidOperationException(startupResult.ErrorMessage ?? "Workflow startup failed");

                    if (startupResult.WorkflowInstance == null)
                        throw new InvalidOperationException("WorkflowEngine returned null instance");

                    return startupResult.WorkflowInstance;
                }, cancellationToken);

                // OUTSIDE TRANSACTION: Continue workflow execution if needed
                if (workflowInstance.Status == WorkflowStatus.Running)
                {
                    var executionResult = await _orchestrator.ExecuteCompleteWorkflowAsync(
                        workflowInstance.Id, 100, cancellationToken);
                    
                    // Update instance with final execution result
                    if (executionResult.WorkflowInstance != null)
                        workflowInstance = executionResult.WorkflowInstance;
                }

                // OUTSIDE TRANSACTION: Publish events after successful commit
                await _eventPublisher.PublishWorkflowStartedAsync(
                    workflowInstance.Id,
                    request.WorkflowDefinitionId,
                    request.InstanceName,
                    request.StartedBy,
                    workflowInstance.StartedOn,
                    request.CorrelationId,
                    cancellationToken);

                _logger.LogInformation("HANDLER: Successfully started workflow instance {WorkflowInstanceId}",
                    workflowInstance.Id);

                return new StartWorkflowResponse
                {
                    WorkflowInstanceId = workflowInstance.Id,
                    InstanceName = workflowInstance.Name,
                    Status = workflowInstance.Status.ToString(),
                    NextActivityId = workflowInstance.CurrentActivityId,
                    NextAssignee = workflowInstance.CurrentAssignee,
                    StartedOn = workflowInstance.StartedOn
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HANDLER: Workflow startup attempt {AttemptNumber} failed for definition {WorkflowDefinitionId}",
                    attemptNumber, request.WorkflowDefinitionId);

                // Create fault context
                var faultContext = new StartWorkflowFaultContext(
                    request.WorkflowDefinitionId,
                    request.InstanceName,
                    request.StartedBy,
                    ex,
                    attemptNumber);

                // Handle the fault
                var faultResult = await _faultHandler.HandleWorkflowStartupFaultAsync(faultContext, cancellationToken);

                if (!faultResult.ShouldRetry || attemptNumber >= maxAttempts)
                {
                    _logger.LogError(ex, "HANDLER: Workflow startup failed permanently after {AttemptNumber} attempts. Reason: {FailureReason}",
                        attemptNumber, faultResult.RecommendedAction);
                    throw;
                }

                if (faultResult.RetryDelay.HasValue)
                {
                    _logger.LogInformation("HANDLER: Retrying workflow startup in {RetryDelay}ms", 
                        faultResult.RetryDelay.Value.TotalMilliseconds);
                    await Task.Delay(faultResult.RetryDelay.Value, cancellationToken);
                }

                attemptNumber++;
            }
        }

        throw new InvalidOperationException($"Workflow startup failed after {maxAttempts} attempts");
    }
}