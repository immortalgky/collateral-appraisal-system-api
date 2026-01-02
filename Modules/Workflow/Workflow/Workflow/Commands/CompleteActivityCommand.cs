using MediatR;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Commands;

/// <summary>
/// Command to complete a specific activity in a workflow
/// </summary>
public sealed record CompleteActivityCommand(
    Guid WorkflowInstanceId,
    string ActivityId,
    string CompletedBy,
    Dictionary<string, object>? OutputData = null,
    string? BookmarkKey = null
) : IRequest<CompleteActivityResult>;

/// <summary>
/// Result of activity completion operation
/// </summary>
public sealed record CompleteActivityResult(
    bool Success,
    WorkflowInstance? WorkflowInstance = null,
    string? NextActivityId = null,
    bool WorkflowCompleted = false,
    string? ErrorMessage = null
);