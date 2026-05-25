namespace Collateral.CollateralMasters.Models;

public class CollateralMasterAuditLog
{
    public Guid Id { get; private set; }
    public Guid CollateralMasterId { get; private set; }
    public string Action { get; private set; } = null!;
    public string? ChangedFields { get; private set; }
    public string Reason { get; private set; } = null!;
    public string ChangedBy { get; private set; } = null!;
    public DateTime ChangedAt { get; private set; }

    private CollateralMasterAuditLog() { }

    public CollateralMasterAuditLog(
        Guid collateralMasterId,
        string action,
        string? changedFields,
        string reason,
        string changedBy,
        DateTime changedAt)
    {
        Id = Guid.CreateVersion7();
        CollateralMasterId = collateralMasterId;
        Action = action;
        ChangedFields = changedFields;
        Reason = reason;
        ChangedBy = changedBy;
        ChangedAt = changedAt;
    }
}
