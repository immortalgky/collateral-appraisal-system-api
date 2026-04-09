using Shared.DDD;
using Workflow.Meetings.Domain.Events;

namespace Workflow.Meetings.Domain;

public class Meeting : Aggregate<Guid>
{
    public string Title { get; private set; } = default!;
    public DateTime? ScheduledAt { get; private set; }
    public string? Location { get; private set; }
    public string? Notes { get; private set; }
    public MeetingStatus Status { get; private set; }
    public string? CancelReason { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    private readonly List<MeetingItem> _items = new();
    public IReadOnlyList<MeetingItem> Items => _items.AsReadOnly();

    private Meeting() { }

    public static Meeting Create(string title, string? notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new Meeting
        {
            Id = Guid.CreateVersion7(),
            Title = title.Trim(),
            Notes = notes,
            Status = MeetingStatus.Draft
        };
    }

    public void UpdateDetails(string title, string? location, string? notes)
    {
        EnsureNotTerminal();
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title.Trim();
        Location = location;
        Notes = notes;
    }

    public void Schedule(DateTime scheduledAt, string? location)
    {
        if (Status != MeetingStatus.Draft && Status != MeetingStatus.Scheduled)
            throw new InvalidOperationException(
                $"Cannot schedule a meeting in status {Status}");

        if (_items.Count == 0)
            throw new InvalidOperationException(
                "Cannot schedule a meeting with no items");

        if (scheduledAt <= DateTime.UtcNow)
            throw new InvalidOperationException(
                "ScheduledAt must be in the future");

        ScheduledAt = scheduledAt;
        if (!string.IsNullOrWhiteSpace(location))
            Location = location;
        Status = MeetingStatus.Scheduled;
    }

    public MeetingItem AddItem(
        Guid appraisalId,
        string? appraisalNo,
        decimal facilityLimit,
        Guid workflowInstanceId,
        string activityId)
    {
        if (Status != MeetingStatus.Draft && Status != MeetingStatus.Scheduled)
            throw new InvalidOperationException(
                $"Cannot add items to a meeting in status {Status}");

        if (_items.Any(i => i.AppraisalId == appraisalId))
            throw new InvalidOperationException(
                $"Appraisal {appraisalId} is already on this meeting");

        var item = MeetingItem.Create(Id, appraisalId, appraisalNo, facilityLimit, workflowInstanceId, activityId);
        _items.Add(item);
        return item;
    }

    public MeetingItem RemoveItem(Guid appraisalId)
    {
        if (Status != MeetingStatus.Draft && Status != MeetingStatus.Scheduled)
            throw new InvalidOperationException(
                $"Cannot remove items from a meeting in status {Status}");

        var item = _items.FirstOrDefault(i => i.AppraisalId == appraisalId)
            ?? throw new InvalidOperationException(
                $"Appraisal {appraisalId} is not on this meeting");

        _items.Remove(item);
        return item;
    }

    public void Cancel(string? reason)
    {
        if (Status != MeetingStatus.Draft && Status != MeetingStatus.Scheduled)
            throw new InvalidOperationException(
                $"Cannot cancel a meeting in status {Status}");

        Status = MeetingStatus.Cancelled;
        CancelReason = reason;
        CancelledAt = DateTime.UtcNow;

        foreach (var item in _items)
        {
            AddDomainEvent(new MeetingCancelledDomainEvent(
                Id, item.AppraisalId, item.WorkflowInstanceId, item.ActivityId, reason));
        }
    }

    public void End()
    {
        if (Status != MeetingStatus.Scheduled)
            throw new InvalidOperationException(
                $"Cannot end a meeting in status {Status}. Meeting must be Scheduled");

        if (_items.Count == 0)
            throw new InvalidOperationException(
                "Cannot end a meeting with no items");

        Status = MeetingStatus.Ended;
        EndedAt = DateTime.UtcNow;

        foreach (var item in _items)
        {
            AddDomainEvent(new MeetingEndedDomainEvent(
                Id, item.AppraisalId, item.WorkflowInstanceId, item.ActivityId));
        }
    }

    private void EnsureNotTerminal()
    {
        if (Status == MeetingStatus.Ended || Status == MeetingStatus.Cancelled)
            throw new InvalidOperationException(
                $"Meeting is in terminal status {Status}");
    }
}
