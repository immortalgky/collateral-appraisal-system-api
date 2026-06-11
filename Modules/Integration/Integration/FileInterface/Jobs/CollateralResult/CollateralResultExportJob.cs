using Collateral.Contracts.FileInterface;
using Hangfire;
using Integration.Contracts.FileInterface;
using Integration.Contracts.FileSink;
using Integration.FileInterface.Format.CollateralResult;
using Microsoft.Extensions.Logging;
using Shared.Time;

namespace Integration.FileInterface.Jobs.CollateralResult;

/// <summary>
/// Hangfire recurring job (several times/day). Builds the outbound "Collateral Result" file from
/// unsent approved (A) and rejected (R) rows. File name and path come from
/// <c>integration.FileInterfaceConfigs</c>. Ledger writes are delegated to <see cref="ICollateralResultLedger"/>.
/// Delivery semantics: at-least-once (file written first, ledger updated after).
/// </summary>
public class CollateralResultExportJob(
    ICollateralResultQuery query,
    CollateralResultFileWriter writer,
    IOutboundFileSink fileSink,
    ICollateralResultLedger ledger,
    IFileInterfaceConfigProvider configProvider,
    IDateTimeProvider dateTimeProvider,
    ILogger<CollateralResultExportJob> logger)
{
    private const string JobTag = "[COLLATERAL-RESULT-EXPORT]";

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        logger.LogInformation("{Tag} Starting export", JobTag);

        var cfg = await configProvider.GetAsync(FileInterfaceCodes.CollateralResult, ct);
        if (cfg is null || !cfg.IsActive)
        {
            logger.LogWarning("{Tag} No active config row for '{Code}'; skipping", JobTag, FileInterfaceCodes.CollateralResult);
            return;
        }

        var rows = await query.GetUnsentRowsAsync(ct);
        if (rows.Count == 0)
        {
            logger.LogInformation("{Tag} No unsent rows (approved or rejected); nothing to send", JobTag);
            return;
        }

        var now = dateTimeProvider.ApplicationNow;
        var effectiveDate = DateOnly.FromDateTime(now);
        var prefix = cfg.FileNamePrefix ?? "COLLATERAL_RESULT_";
        var dateFormat = cfg.FileNameDateFormat ?? "yyyyMMddHHmmss";
        var ext = cfg.FileExtension ?? "txt";
        var directory = cfg.Directory ?? "./outbound";
        var fileName = $"{prefix}{now.ToString(dateFormat)}.{ext}";

        var content = writer.BuildContent(effectiveDate, rows);

        // Write/upload first; only record the ledger on success (at-least-once).
        await fileSink.WriteAsync(directory, fileName, content, ct);

        await ledger.MarkSentAsync(rows, fileName, now, ct);

        logger.LogInformation(
            "{Tag} Sent {Total} record(s) in {File} in {Dir}",
            JobTag, rows.Count, fileName, directory);
    }
}
