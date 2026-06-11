namespace Collateral.Contracts.FileInterface;

/// <summary>
/// Marks collateral result rows as sent after the export job successfully writes the file.
/// Implementations write to both the <c>CollateralResultLogs</c> (A rows) and the
/// <c>PendingCollateralResults</c> spool (R rows), then commit.
/// </summary>
public interface ICollateralResultLedger
{
    Task MarkSentAsync(
        IReadOnlyList<CollateralResultRow> sent,
        string fileName,
        DateTime sentAt,
        CancellationToken cancellationToken = default);
}
