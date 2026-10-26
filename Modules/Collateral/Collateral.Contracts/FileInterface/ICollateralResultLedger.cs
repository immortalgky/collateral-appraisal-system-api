namespace Collateral.Contracts.FileInterface;

/// <summary>
/// Marks collateral result rows as sent after the export job successfully writes the file.
/// Implementations write one <c>CollateralResultLogs</c> row per sent row, then commit.
/// </summary>
public interface ICollateralResultLedger
{
    Task MarkSentAsync(
        IReadOnlyList<CollateralResultRow> sent,
        string fileName,
        DateTime sentAt,
        CancellationToken cancellationToken = default);
}
