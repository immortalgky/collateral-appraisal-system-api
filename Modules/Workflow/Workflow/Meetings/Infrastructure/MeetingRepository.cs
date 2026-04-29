using Microsoft.EntityFrameworkCore;
using Workflow.Data;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Infrastructure;

public class MeetingRepository(WorkflowDbContext dbContext) : IMeetingRepository
{
    public Task<Meeting?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return dbContext.Meetings
            .Include(m => m.Members)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public Task<Meeting?> GetByIdWithItemsAsync(Guid id, CancellationToken ct = default)
    {
        return dbContext.Meetings
            .Include(m => m.Items)
            .Include(m => m.Members)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    /// <summary>
    /// Loads meeting with both Items and Members — required for Release/RouteBack
    /// and for meeting detail views that need member user IDs.
    /// </summary>
    public Task<Meeting?> GetByIdForDecisionAsync(Guid id, CancellationToken ct = default)
    {
        return dbContext.Meetings
            .Include(m => m.Items)
            .Include(m => m.Members)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public Task<Meeting?> GetEarlierUnendedMeetingAsync(int meetingNoYear, int meetingNoSeq, CancellationToken ct = default)
    {
        return dbContext.Meetings
            .AsNoTracking()
            .Where(m => m.MeetingNoYear == meetingNoYear
                     && m.MeetingNoSeq < meetingNoSeq
                     && m.Status != MeetingStatus.Ended
                     && m.Status != MeetingStatus.Cancelled)
            .OrderBy(m => m.MeetingNoSeq)
            .FirstOrDefaultAsync(ct);
    }

    public Task<Meeting?> GetOverlappingMeetingAsync(DateTime startAt, DateTime endAt, Guid? excludeMeetingId = null, CancellationToken ct = default)
    {
        // Half-open interval intersection: existing.StartAt < endAt && existing.EndAt > startAt.
        // Cancelled meetings are ignored — they no longer occupy the slot.
        return dbContext.Meetings
            .AsNoTracking()
            .Where(m => m.Status != MeetingStatus.Cancelled
                     && m.StartAt != null
                     && m.EndAt != null
                     && m.StartAt < endAt
                     && m.EndAt > startAt
                     && (excludeMeetingId == null || m.Id != excludeMeetingId))
            .OrderBy(m => m.StartAt)
            .FirstOrDefaultAsync(ct);
    }

    public Task<Meeting?> GetLatestScheduledMeetingAsync(CancellationToken ct = default)
    {
        return dbContext.Meetings
            .AsNoTracking()
            .Where(m => m.Status != MeetingStatus.Cancelled
                     && m.StartAt != null)
            .OrderByDescending(m => m.StartAt)
            .FirstOrDefaultAsync(ct);
    }

    public Task<Meeting?> GetLatestScheduledBeforeNumberAsync(int meetingNoYear, int meetingNoSeq, CancellationToken ct = default)
    {
        // Lower-numbered ⇔ (Year < target.Year) || (Year == target.Year && Seq < target.Seq).
        return dbContext.Meetings
            .AsNoTracking()
            .Where(m => m.Status != MeetingStatus.Cancelled
                     && m.StartAt != null
                     && m.MeetingNoYear != null
                     && m.MeetingNoSeq != null
                     && (m.MeetingNoYear < meetingNoYear
                         || (m.MeetingNoYear == meetingNoYear && m.MeetingNoSeq < meetingNoSeq)))
            .OrderByDescending(m => m.StartAt)
            .FirstOrDefaultAsync(ct);
    }

    public Task<Meeting?> GetEarliestScheduledAfterNumberAsync(int meetingNoYear, int meetingNoSeq, CancellationToken ct = default)
    {
        // Higher-numbered ⇔ (Year > target.Year) || (Year == target.Year && Seq > target.Seq).
        return dbContext.Meetings
            .AsNoTracking()
            .Where(m => m.Status != MeetingStatus.Cancelled
                     && m.StartAt != null
                     && m.MeetingNoYear != null
                     && m.MeetingNoSeq != null
                     && (m.MeetingNoYear > meetingNoYear
                         || (m.MeetingNoYear == meetingNoYear && m.MeetingNoSeq > meetingNoSeq)))
            .OrderBy(m => m.StartAt)
            .FirstOrDefaultAsync(ct);
    }

    public Task AddAsync(Meeting meeting, CancellationToken ct = default)
    {
        dbContext.Meetings.Add(meeting);
        return Task.CompletedTask;
    }
}
