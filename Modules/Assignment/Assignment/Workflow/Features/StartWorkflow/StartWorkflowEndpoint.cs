using Assignment.Workflow.Services;
using Assignment.Workflow.Activities.Core;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Assignment.Workflow.Features.StartWorkflow;

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
    private readonly IWorkflowService _workflowService;

    public StartWorkflowCommandHandler(IWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    public async Task<StartWorkflowResponse> Handle(StartWorkflowCommand request, CancellationToken cancellationToken)
    {
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

        var workflowInstance = await _workflowService.StartWorkflowAsync(
            request.WorkflowDefinitionId,
            request.InstanceName,
            request.StartedBy,
            request.InitialVariables,
            request.CorrelationId,
            runtimeOverrides,
            cancellationToken);

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
}