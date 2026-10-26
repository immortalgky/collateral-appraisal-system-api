using Collateral.CollateralMasters.Models;
using Collateral.Contracts.FileInterface;
using Collateral.Data;
using Microsoft.Extensions.Logging;

namespace Collateral.CollateralMasters.CollateralResult;

/// <summary>
/// Marks rows as sent after the export job successfully writes the file: inserts one
/// <see cref="CollateralResultLog"/> per row, in a single SaveChangesAsync call.
/// </summary>
public class CollateralResultLedger(
    CollateralDbContext dbContext,
    ILogger<CollateralResultLedger> logger) : ICollateralResultLedger
{
    public async Task MarkSentAsync(
        IReadOnlyList<CollateralResultRow> sent,
        string fileName,
        DateTime sentAt,
        CancellationToken cancellationToken = default)
    {
        foreach (var row in sent)
        {
            dbContext.CollateralResultLogs.Add(new CollateralResultLog(
                appraisalId: row.AppraisalId,
                appraisalNumber: row.AppraisalReportNumber,
                collateralId: row.CollateralId,
                sentAt: sentAt,
                fileName: fileName));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[CollateralResultLedger] Marked {Total} row(s) sent in {File}",
            sent.Count, fileName);
    }
}
