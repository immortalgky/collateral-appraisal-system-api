using Collateral.CollateralMasters.Models;
using Collateral.Contracts.FileInterface;
using Collateral.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Collateral.CollateralMasters.CollateralResult;

/// <summary>
/// Marks rows as sent after the export job successfully writes the file.
/// - A rows  → insert a new <see cref="CollateralResultLog"/> row.
/// - R rows  → stamp SentAt on the matching <see cref="PendingCollateralResult"/> spool row.
/// Both writes happen in a single SaveChangesAsync call (same transaction).
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
        var aRows = sent.Where(r => r.AppraisalStatus == "A").ToList();
        var rIds  = sent.Where(r => r.AppraisalStatus == "R").Select(r => r.AppraisalId).ToList();

        // A rows → insert CollateralResultLog (existing behaviour).
        foreach (var row in aRows)
        {
            dbContext.CollateralResultLogs.Add(new CollateralResultLog(
                appraisalId: row.AppraisalId,
                appraisalNumber: row.AppraisalReportNumber,
                collateralId: row.CollateralId,
                sentAt: sentAt,
                fileName: fileName));
        }

        // R rows → stamp SentAt on the PendingCollateralResult spool rows.
        if (rIds.Count > 0)
        {
            var pendingRows = await dbContext.PendingCollateralResults
                .Where(p => rIds.Contains(p.AppraisalId))
                .ToListAsync(cancellationToken);

            foreach (var pending in pendingRows)
            {
                pending.MarkSent(sentAt, fileName);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[CollateralResultLedger] Marked {Total} row(s) sent in {File} (A={ACount}, R={RCount})",
            sent.Count, fileName, aRows.Count, rIds.Count);
    }
}
