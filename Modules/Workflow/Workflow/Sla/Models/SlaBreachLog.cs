using Shared.DDD;

namespace Workflow.Sla.Models;

public class SlaBreachLog : Entity<Guid>
{
    public Guid PendingTaskId { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string TaskName { get; private set; } = default!;
    public string AssignedTo { get; private set; } = default!;
    public DateTime DueAt { get; private set; }
    public DateTime BreachedAt { get; private set; }
    public DateTime? NotifiedAt { get; private set; }
    public string SlaStatus { get; private set; } = default!;

    private SlaBreachLog() { }

    public static SlaBreachLog Create(
        Guid pendingTaskId,
        Guid correlationId,
        string taskName,
        string assignedTo,
        DateTime dueAt,
        DateTime breachedAt,
        string slaStatus)
    {
        return new SlaBreachLog
        {
            Id = Guid.CreateVersion7(),
            PendingTaskId = pendingTaskId,
            CorrelationId = correlationId,
            TaskName = taskName,
            AssignedTo = assignedTo,
            DueAt = dueAt,
            BreachedAt = breachedAt,
            SlaStatus = slaStatus
        };
    }

    public void MarkNotified(DateTime notifiedAt)
    {
        NotifiedAt = notifiedAt;
    }
}
