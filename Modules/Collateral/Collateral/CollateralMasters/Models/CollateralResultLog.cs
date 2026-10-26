namespace Collateral.CollateralMasters.Models;

/// <summary>
/// Sent-ledger for the outbound Collateral Result interface. One row per row shipped to the host (AS400):
/// usually one per appraisal, one per matched unit for a block project. The export view selects completed
/// appraisals with NO row here (a NOT EXISTS on AppraisalId), so a collateral completing AFTER a run is picked
/// up by the next run, and that NOT EXISTS is what stops a resend. The unique index on (AppraisalId,
/// CollateralId) is only the row key that lets a block hold one row per unit.
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
