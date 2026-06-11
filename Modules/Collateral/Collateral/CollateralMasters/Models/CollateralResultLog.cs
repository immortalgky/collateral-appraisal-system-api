namespace Collateral.CollateralMasters.Models;

/// <summary>
/// Sent-ledger for the outbound Collateral Result interface. One row per appraisal that has been
/// shipped to the host (AS400). The export job selects completed appraisals NOT present here, so a
/// collateral completing AFTER a run is picked up by the next run (late-completion safety). The
/// unique index on AppraisalId is the idempotency guard against double-sends.
/// </summary>
public class CollateralResultLog
{
    public Guid Id { get; private set; }
    public Guid AppraisalId { get; private set; }
    public string AppraisalNumber { get; private set; } = null!;
    public string CollateralId { get; private set; } = null!;
    public DateTime SentAt { get; private set; }
    public string FileName { get; private set; } = null!;

    private CollateralResultLog() { }

    public CollateralResultLog(
        Guid appraisalId,
        string appraisalNumber,
        string collateralId,
        DateTime sentAt,
        string fileName)
    {
        Id = Guid.CreateVersion7();
        AppraisalId = appraisalId;
        AppraisalNumber = appraisalNumber;
        CollateralId = collateralId;
        SentAt = sentAt;
        FileName = fileName;
    }
}
