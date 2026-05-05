namespace Collateral.CollateralMasters.Models;

public class CollateralBackfillReport
{
    public Guid Id { get; private set; }
    public Guid AppraisalId { get; private set; }
    public string Status { get; private set; } = null!;
    public string? Message { get; private set; }
    public DateTime RunAt { get; private set; }

    private CollateralBackfillReport() { }

    public CollateralBackfillReport(Guid appraisalId, string status, string? message)
    {
        Id = Guid.CreateVersion7();
        AppraisalId = appraisalId;
        Status = status;
        Message = message;
        RunAt = DateTime.UtcNow;
    }
}
