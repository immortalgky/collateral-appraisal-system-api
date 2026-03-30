namespace Workflow.Domain.Committees;

public interface ICommitteeRepository
{
    Task<Committee?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Committee?> GetByIdWithMembersAsync(Guid id, CancellationToken ct = default);
    Task<List<Committee>> GetActiveCommitteesAsync(CancellationToken ct = default);
    Task<Committee?> GetCommitteeForValueAsync(decimal value, CancellationToken ct = default);
    Task AddAsync(Committee committee, CancellationToken ct = default);
    Task UpdateAsync(Committee committee, CancellationToken ct = default);
}
