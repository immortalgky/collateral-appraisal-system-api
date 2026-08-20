namespace Collateral.CollateralMasters.Models;

/// <summary>
/// Spool row for outbound "R" (rejected) records in the Collateral Result interface.
/// Written by <c>AppraisalRejectedConsumer</c> when an appraisal is rejected by committee;
/// read and marked sent by <c>CollateralResultExportJob</c> on the next run.
///
/// <see cref="HostCollateralId"/> (CCDCID) is always NULL, and that is correct: AS400 mints a
/// collateral id at drawdown, which a rejected appraisal never reaches, so there is never an id to
/// receive. R rows therefore go out with a blank CCDCID, which AS400 accepts because AppraisalNumber
/// is the join key.
///
/// Idempotency: <see cref="AppraisalId"/> has a unique index; duplicate delivery is suppressed
/// by the InboxGuard on the consumer (first-write wins).
/// </summary>
public class PendingCollateralResult
{
    public Guid Id { get; private set; }
    public Guid AppraisalId { get; private set; }
    public string AppraisalNumber { get; private set; } = null!;

    /// <summary>
    /// Always NULL — a rejected appraisal never reaches drawdown, so AS400 never issues an id.
    /// The column is kept only so the outbound file layout stays complete per the contract.
    /// </summary>
    public string? HostCollateralId { get; private set; }

    public DateTime RejectedAt { get; private set; }

    /// <summary>
    /// NULL until the export job writes and marks this row sent.
    /// Non-null = already included in a file (at-least-once; AS400 upserts by AppraisalNumber).
    /// </summary>
    public DateTime? SentAt { get; private set; }

    /// <summary>
    /// Name of the file this row was included in. Set alongside <see cref="SentAt"/>.
    /// </summary>
    public string? SentFileName { get; private set; }

    private PendingCollateralResult() { }

    public static PendingCollateralResult Create(
        Guid appraisalId,
        string appraisalNumber,
        string? hostCollateralId,
        DateTime rejectedAt)
    {
        return new PendingCollateralResult
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            AppraisalNumber = appraisalNumber,
            HostCollateralId = string.IsNullOrWhiteSpace(hostCollateralId) ? null : hostCollateralId.Trim(),
            RejectedAt = rejectedAt,
            SentAt = null,
            SentFileName = null
        };
    }

    /// <summary>
    /// Stamps this spool row as sent. Called by the export job after successfully writing the file.
    /// </summary>
    public void MarkSent(DateTime sentAt, string fileName)
    {
        SentAt = sentAt;
        SentFileName = fileName;
    }
}
