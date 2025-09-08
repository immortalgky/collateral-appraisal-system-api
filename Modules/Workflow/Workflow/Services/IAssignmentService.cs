using Shared.Messaging.Values;

namespace Workflow.Services;

public interface IAssignmentService
{
    Task<Guid> StartWorkflowAsync(long requestId, CancellationToken cancellationToken = default);

    Task CompleteTaskAsync(Guid correlationId, TaskName taskName, string actionTaken,
        CancellationToken cancellationToken = default);

    Task AssignTaskAsync(Guid correlationId, TaskName taskName, CancellationToken cancellationToken = default);
}