using Carter;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace Notification.Notification.Features.TestNotifications;

public class TestNotificationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/test/notifications")
            .WithTags("Test Notifications");

        group.MapPost("/task-completed", SimulateTaskCompletion)
            .WithName("SimulateTaskCompletion")
            .WithSummary("Simulate a task completion event")
            .WithDescription("Publishes a TaskCompleted event for testing notifications");

        group.MapPost("/task-assigned", SimulateTaskAssignment)
            .WithName("SimulateTaskAssignment")
            .WithSummary("Simulate a task assignment event")
            .WithDescription("Publishes a TaskAssigned event for testing notifications");

        group.MapPost("/transition-completed", SimulateTransitionCompleted)
            .WithName("SimulateTransitionCompleted")
            .WithSummary("Simulate a workflow transition")
            .WithDescription("Publishes a TransitionCompleted event for testing notifications");
    }

    private static async Task<IResult> SimulateTaskCompletion(
        [FromBody] SimulateTaskCompletionRequest request,
        [FromServices] IBus bus)
    {
        var taskCompleted = new TaskCompleted
        {
            CorrelationId = request.CorrelationId ?? Guid.NewGuid(),
            TaskName = request.TaskName ?? "Admin",
            ActionTaken = request.ActionTaken ?? "P"
        };

        await bus.Publish(taskCompleted);
        
        return Results.Ok(new { 
            Message = "TaskCompleted event published", 
            Event = taskCompleted 
        });
    }

    private static async Task<IResult> SimulateTaskAssignment(
        [FromBody] SimulateTaskAssignmentRequest request,
        [FromServices] IBus bus)
    {
        var taskAssigned = new TaskAssigned
        {
            CorrelationId = request.CorrelationId ?? Guid.NewGuid(),
            TaskName = request.TaskName ?? "AppraisalStaff",
            AssignedTo = request.AssignedTo ?? "testuser",
            AssignedType = request.AssignedType ?? "U"
        };

        await bus.Publish(taskAssigned);
        
        return Results.Ok(new { 
            Message = "TaskAssigned event published", 
            Event = taskAssigned 
        });
    }

    private static async Task<IResult> SimulateTransitionCompleted(
        [FromBody] SimulateTransitionCompletedRequest request,
        [FromServices] IBus bus)
    {
        var transitionCompleted = new TransitionCompleted
        {
            CorrelationId = request.CorrelationId ?? Guid.NewGuid(),
            RequestId = request.RequestId ?? 1,
            TaskName = request.TaskName ?? "Admin",
            CurrentState = request.CurrentState ?? "Admin",
            AssignedTo = request.AssignedTo ?? "testuser",
            AssignedType = request.AssignedType ?? "U"
        };

        await bus.Publish(transitionCompleted);
        
        return Results.Ok(new { 
            Message = "TransitionCompleted event published", 
            Event = transitionCompleted 
        });
    }
}

public record SimulateTaskCompletionRequest(
    Guid? CorrelationId = null,
    string? TaskName = null,
    string? ActionTaken = null
);

public record SimulateTaskAssignmentRequest(
    Guid? CorrelationId = null,
    string? TaskName = null,
    string? AssignedTo = null,
    string? AssignedType = null
);

public record SimulateTransitionCompletedRequest(
    Guid? CorrelationId = null,
    long? RequestId = null,
    string? TaskName = null,
    string? CurrentState = null,
    string? AssignedTo = null,
    string? AssignedType = null
);