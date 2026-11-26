namespace Appraisal.Domain.Appraisals;

/// <summary>Append-only audit of suspend/reactivate actions. Current state is Appraisal.IsSuspended.</summary>
public class AppraisalSuspensionLog
{
    public const string ActionSuspended = "Suspended";
    public const string ActionReactivated = "Reactivated";

    public Guid Id { get; private set; }
    public Guid AppraisalId { get; private set; }
    public string Action { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public DateTime ActionAt { get; private set; }
    public string ActionBy { get; private set; } = null!;

    private AppraisalSuspensionLog()
    {
        // For EF Core
    }

    private AppraisalSuspensionLog(
        Guid appraisalId,
        string action,
        string reason,
        DateTime actionAt,
        string actionBy
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionBy);
        Id = Guid.CreateVersion7();
        AppraisalId = appraisalId;
        Action = action;
        Reason = reason.Trim();
        ActionAt = actionAt;
        ActionBy = actionBy;
    }

    public static AppraisalSuspensionLog ForSuspend(Guid appraisalId,
        string reason,
        DateTime actionAt,
        string actionBy) => new(appraisalId, ActionSuspended, reason, actionAt, actionBy);

    public static AppraisalSuspensionLog ForReactivate(Guid appraisalId,
        string reason,
        DateTime actionAt,
        string actionBy) => new(appraisalId, ActionReactivated, reason, actionAt, actionBy);
}