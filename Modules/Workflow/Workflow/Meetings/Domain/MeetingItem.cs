using Shared.DDD;

namespace Workflow.Meetings.Domain;

public class MeetingItem : Entity<Guid>
{
    public Guid MeetingId { get; private set; }
    public Guid AppraisalId { get; private set; }
    public string? AppraisalNo { get; private set; }
    public decimal FacilityLimit { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public string ActivityId { get; private set; } = default!;
    public DateTime AddedAt { get; private set; }

    private MeetingItem() { }

    internal static MeetingItem Create(
        Guid meetingId,
        Guid appraisalId,
        string? appraisalNo,
        decimal facilityLimit,
        Guid workflowInstanceId,
        string activityId)
    {
        return new MeetingItem
        {
            Id = Guid.CreateVersion7(),
            MeetingId = meetingId,
            AppraisalId = appraisalId,
            AppraisalNo = appraisalNo,
            FacilityLimit = facilityLimit,
            WorkflowInstanceId = workflowInstanceId,
            ActivityId = activityId,
            AddedAt = DateTime.UtcNow
        };
    }
}
