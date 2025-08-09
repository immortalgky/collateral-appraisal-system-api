using Assignment.Workflow.Engine;
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
                StartedBy = request.StartedBy, // In real app, get from current user context
                InitialVariables = request.InitialVariables,
                CorrelationId = request.CorrelationId
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
}

public record StartWorkflowCommand : IRequest<StartWorkflowResponse>
{
    public Guid WorkflowDefinitionId { get; init; }
    public string InstanceName { get; init; } = default!;
    public string StartedBy { get; init; } = default!;
    public Dictionary<string, object>? InitialVariables { get; init; }
    public string? CorrelationId { get; init; }
}

public record StartWorkflowResponse
{
    public Guid WorkflowInstanceId { get; init; }
    public string InstanceName { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string CurrentActivityId { get; init; } = default!;
    public DateTime StartedOn { get; init; }
}

public class StartWorkflowCommandHandler : IRequestHandler<StartWorkflowCommand, StartWorkflowResponse>
{
    private readonly IWorkflowEngine _workflowEngine;

    public StartWorkflowCommandHandler(IWorkflowEngine workflowEngine)
    {
        _workflowEngine = workflowEngine;
    }

    public async Task<StartWorkflowResponse> Handle(StartWorkflowCommand request, CancellationToken cancellationToken)
    {
        var workflowInstance = await _workflowEngine.StartWorkflowAsync(
            request.WorkflowDefinitionId,
            request.InstanceName,
            request.StartedBy,
            request.InitialVariables,
            request.CorrelationId,
            cancellationToken);

        return new StartWorkflowResponse
        {
            WorkflowInstanceId = workflowInstance.Id,
            InstanceName = workflowInstance.Name,
            Status = workflowInstance.Status.ToString(),
            CurrentActivityId = workflowInstance.CurrentActivityId,
            StartedOn = workflowInstance.StartedOn
        };
    }
}