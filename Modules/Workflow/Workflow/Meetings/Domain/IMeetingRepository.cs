namespace Workflow.Meetings.Domain;

public interface IMeetingRepository
{
    Task<Meeting?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Meeting?> GetByIdWithItemsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Loads meeting with Items and Members — for Release/RouteBack and detail views.</summary>
    Task<Meeting?> GetByIdForDecisionAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Meeting meeting, CancellationToken ct = default);
}
