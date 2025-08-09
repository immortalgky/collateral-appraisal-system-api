using Assignment.Workflow.Engine;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Assignment.Workflow.Features.CompleteActivity;

public class CompleteActivityEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workflows/instances/{workflowInstanceId:guid}/activities/{activityId}/complete", async (
            Guid workflowInstanceId,
            string activityId,
            CompleteActivityRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new CompleteActivityCommand
            {
                WorkflowInstanceId = workflowInstanceId,
                ActivityId = activityId,
                OutputData = request.OutputData,
                CompletedBy = request.CompletedBy, // In real app, get from current user context
                Comments = request.Comments
            };
            
            var result = await sender.Send(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("CompleteActivity")
        .WithTags("Workflows");
    }
}

public record CompleteActivityRequest
{
    public Dictionary<string, object> OutputData { get; init; } = new();
    public string CompletedBy { get; init; } = default!;
    public string? Comments { get; init; }
}

public record CompleteActivityCommand : IRequest<CompleteActivityResponse>
{
    public Guid WorkflowInstanceId { get; init; }
    public string ActivityId { get; init; } = default!;
    public Dictionary<string, object> OutputData { get; init; } = new();
    public string CompletedBy { get; init; } = default!;
    public string? Comments { get; init; }
}

public record CompleteActivityResponse
{
    public Guid WorkflowInstanceId { get; init; }
    public string Status { get; init; } = default!;
    public string CurrentActivityId { get; init; } = default!;
    public string? NextActivityId { get; init; }
    public bool IsCompleted { get; init; }
}

public class CompleteActivityCommandHandler : IRequestHandler<CompleteActivityCommand, CompleteActivityResponse>
{
    private readonly IWorkflowEngine _workflowEngine;

    public CompleteActivityCommandHandler(IWorkflowEngine workflowEngine)
    {
        _workflowEngine = workflowEngine;
    }

    public async Task<CompleteActivityResponse> Handle(CompleteActivityCommand request, CancellationToken cancellationToken)
    {
        var workflowInstance = await _workflowEngine.ResumeWorkflowAsync(
            request.WorkflowInstanceId,
            request.ActivityId,
            request.OutputData,
            request.CompletedBy,
            request.Comments,
            cancellationToken);

        return new CompleteActivityResponse
        {
            WorkflowInstanceId = workflowInstance.Id,
            Status = workflowInstance.Status.ToString(),
            CurrentActivityId = workflowInstance.CurrentActivityId,
            NextActivityId = request.OutputData.ContainsKey("nextActivityId") ? request.OutputData["nextActivityId"]?.ToString() : null,
            IsCompleted = workflowInstance.Status == Models.WorkflowStatus.Completed
        };
    }
}