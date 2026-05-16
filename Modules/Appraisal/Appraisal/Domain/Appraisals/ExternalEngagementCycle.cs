namespace Appraisal.Domain.Appraisals;

public class ExternalEngagementCycle : Entity<Guid>
{
    public Guid AppraisalAssignmentId { get; private set; }
    public int CycleNumber { get; private set; }
    public DateTime OpenedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public int? BusinessMinutes { get; private set; }
    public CycleStatus Status { get; private set; } = CycleStatus.Open;

    private ExternalEngagementCycle() { }

    public static ExternalEngagementCycle Open(Guid assignmentId, int cycleNumber, DateTime openedAt)
    {
        return new ExternalEngagementCycle
        {
            Id = Guid.CreateVersion7(),
            AppraisalAssignmentId = assignmentId,
            CycleNumber = cycleNumber,
            OpenedAt = openedAt,
            Status = CycleStatus.Open
        };
    }

    public void Close(DateTime closedAt, int businessMinutes)
    {
        if (Status == CycleStatus.Closed) return;
        ClosedAt = closedAt;
        BusinessMinutes = businessMinutes;
        Status = CycleStatus.Closed;
    }
}
