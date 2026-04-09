namespace Workflow.DocumentFollowups.Application;

/// <summary>
/// Gating service used by the workflow engine to block submission of a checker task
/// while it has open document followups.
/// </summary>
public interface IDocumentFollowupGate
{
    Task<bool> HasOpenFollowupAsync(Guid raisingPendingTaskId, CancellationToken cancellationToken = default);
}
