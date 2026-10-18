namespace Collateral.CollateralMasters.Models;

/// <summary>
/// Spool row recording that an appraisal was rejected by committee, written by
/// <c>AppraisalRejectedConsumer</c>. The Collateral Result export no longer reads or marks it:
/// rejections are still spooled, but they are not exported ("R" rows are not sent), so
/// <see cref="SentAt"/> stays NULL for new rows (rows the old export marked keep their value).
///
/// <see cref="HostCollateralId"/> is always NULL, and that is correct: AS400 mints a collateral id at
/// drawdown, which a rejected appraisal never reaches.
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
    /// Nothing reads it now that the export does not send rejected appraisals.
    /// </summary>
    public string? HostCollateralId { get; private set; }

    public DateTime RejectedAt { get; private set; }

    /// <summary>
    /// NULL for every row spooled since the Collateral Result export stopped sending rejected appraisals: nothing
    /// reads or marks this spool any more. Rows the old export had already marked keep their value. The column is
    /// kept, with its index, only to avoid a migration.
    /// </summary>
    public DateTime? SentAt { get; private set; }

    /// <summary>
    /// NULL for rows spooled since the export stopped sending them, like <see cref="SentAt"/>; rows the old export
    /// had already marked keep the file name.
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
}
