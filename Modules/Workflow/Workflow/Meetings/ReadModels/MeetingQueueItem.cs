using Shared.DDD;

namespace Workflow.Meetings.ReadModels;

public enum MeetingQueueItemStatus
{
    Queued,
    Assigned,
    Released
}

public class MeetingQueueItem : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }
    public string? AppraisalNo { get; private set; }
    public decimal FacilityLimit { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public string ActivityId { get; private set; } = default!;
    public Guid? MeetingId { get; private set; }
    public MeetingQueueItemStatus Status { get; private set; }
    public DateTime EnqueuedAt { get; private set; }

    private MeetingQueueItem() { }

    public static MeetingQueueItem CreateQueued(
        Guid appraisalId,
        string? appraisalNo,
        decimal facilityLimit,
        Guid workflowInstanceId,
        string activityId)
    {
        return new MeetingQueueItem
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            AppraisalNo = appraisalNo,
            FacilityLimit = facilityLimit,
            WorkflowInstanceId = workflowInstanceId,
            ActivityId = activityId,
            Status = MeetingQueueItemStatus.Queued,
            EnqueuedAt = DateTime.Now
        };
    }

    public void AssignTo(Guid meetingId)
    {
        if (Status != MeetingQueueItemStatus.Queued)
            throw new InvalidOperationException(
                $"Cannot assign queue item in status {Status} to a meeting");
        MeetingId = meetingId;
        Status = MeetingQueueItemStatus.Assigned;
    }

    public void ReturnToQueue()
    {
        if (Status != MeetingQueueItemStatus.Assigned)
            throw new InvalidOperationException(
                $"Cannot return queue item in status {Status} to queue");
        MeetingId = null;
        Status = MeetingQueueItemStatus.Queued;
    }

    public void Release()
    {
        if (Status != MeetingQueueItemStatus.Assigned)
            throw new InvalidOperationException(
                $"Cannot release queue item in status {Status}");
        Status = MeetingQueueItemStatus.Released;
    }
}
