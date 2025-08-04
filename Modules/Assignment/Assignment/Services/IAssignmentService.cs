namespace Assignment.Services;

public interface IAssignmentService
{
    Task<Guid> StartWorkflowAsync(long requestId, CancellationToken cancellationToken = default);

    Task CompleteTaskAsync(Guid correlationId, string taskName, string actionTaken,
        CancellationToken cancellationToken = default);

    Task AssignTaskAsync(Guid correlationId, string taskName, CancellationToken cancellationToken = default);
}