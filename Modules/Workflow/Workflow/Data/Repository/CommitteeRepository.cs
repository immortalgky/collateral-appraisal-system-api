using Workflow.Domain.Committees;

namespace Workflow.Data.Repository;

public class CommitteeRepository(WorkflowDbContext dbContext) : ICommitteeRepository
{
    public async Task<Committee?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        return await dbContext.Committees
            .Include(c => c.Members)
            .Include(c => c.Thresholds)
            .Include(c => c.Conditions)
            .FirstOrDefaultAsync(c => c.Code == code, ct);
    }

    public async Task<Committee?> GetByIdWithMembersAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.Committees
            .Include(c => c.Members)
            .Include(c => c.Thresholds)
            .Include(c => c.Conditions)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<List<Committee>> GetActiveCommitteesAsync(CancellationToken ct = default)
    {
        return await dbContext.Committees
            .Include(c => c.Members)
            .Include(c => c.Thresholds)
            .Include(c => c.Conditions)
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<Committee?> GetCommitteeForValueAsync(decimal value, CancellationToken ct = default)
    {
        // Find the threshold that matches the value, ordered by priority
        var threshold = await dbContext.Set<CommitteeThreshold>()
            .Where(t => t.IsActive
                && (t.MinValue == null || value >= t.MinValue)
                && (t.MaxValue == null || value <= t.MaxValue))
            .OrderBy(t => t.Priority)
            .FirstOrDefaultAsync(ct);

        if (threshold is null) return null;

        return await GetByIdWithMembersAsync(threshold.CommitteeId, ct);
    }

    public Task AddAsync(Committee committee, CancellationToken ct = default)
    {
        dbContext.Committees.Add(committee);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Committee committee, CancellationToken ct = default)
    {
        dbContext.Committees.Update(committee);
        return Task.CompletedTask;
    }
}
