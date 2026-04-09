using Microsoft.EntityFrameworkCore;
using Workflow.Data;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Infrastructure;

public class MeetingRepository(WorkflowDbContext dbContext) : IMeetingRepository
{
    public Task<Meeting?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return dbContext.Meetings.FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public Task<Meeting?> GetByIdWithItemsAsync(Guid id, CancellationToken ct = default)
    {
        return dbContext.Meetings
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public Task AddAsync(Meeting meeting, CancellationToken ct = default)
    {
        dbContext.Meetings.Add(meeting);
        return Task.CompletedTask;
    }
}
