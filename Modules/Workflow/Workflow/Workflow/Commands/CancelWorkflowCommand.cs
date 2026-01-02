using MediatR;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Commands;

/// <summary>
/// Command to cancel a running workflow instance
/// </summary>
public sealed record CancelWorkflowCommand(
    Guid WorkflowInstanceId,
    string CancelledBy,
    string? Reason = null
) : IRequest<CancelWorkflowResult>;

/// <summary>
/// Result of workflow cancellation operation
/// </summary>
public sealed record CancelWorkflowResult(
    bool Success,
    WorkflowInstance? WorkflowInstance = null,
    string? ErrorMessage = null
);