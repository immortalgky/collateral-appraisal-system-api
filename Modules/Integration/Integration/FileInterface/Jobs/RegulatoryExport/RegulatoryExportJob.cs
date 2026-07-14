using Collateral.Contracts.FileInterface;
using Hangfire;
using Integration.Contracts.FileInterface;
using Integration.Contracts.FileSink;
using Integration.FileInterface.Format.RegulatoryExport;
using Microsoft.Extensions.Logging;
using Shared.Time;

namespace Integration.FileInterface.Jobs.RegulatoryExport;

/// <summary>
/// Hangfire monthly recurring job. Builds the outbound "CAS-AS400-Regulatory" file from a full
/// Basel/RDT regulatory snapshot. File name and path come from <c>integration.FileInterfaceConfigs</c>.
/// No sent-ledger: every run is a full re-extract.
/// </summary>
public class RegulatoryExportJob(
    IRegulatoryExportQuery query,
    RegulatoryFileWriter writer,
    RegulatoryExcelWriter excelWriter,
    IOutboundFileSink fileSink,
    IFileInterfaceConfigProvider configProvider,
    IDateTimeProvider dateTimeProvider,
    ILogger<RegulatoryExportJob> logger)
{
    private const string JobTag = "[REGULATORY-EXPORT]";

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        logger.LogInformation("{Tag} Starting monthly regulatory export", JobTag);

        var cfg = await configProvider.GetAsync(FileInterfaceCodes.Regulatory, ct);
        if (cfg is null || !cfg.IsActive)
        {
            logger.LogWarning("{Tag} No active config row for '{Code}'; skipping", JobTag, FileInterfaceCodes.Regulatory);
            return;
        }

        var rows = await query.GetRowsAsync(ct);
        if (rows.Count == 0)
        {
            logger.LogInformation("{Tag} No active collateral masters found; nothing to send", JobTag);
            return;
        }

        var now = dateTimeProvider.ApplicationNow;
        var effectiveDate = DateOnly.FromDateTime(now);
        var prefix = cfg.FileNamePrefix ?? "REGULATORY_";
        var dateFormat = cfg.FileNameDateFormat ?? "yyyyMMdd";
        var ext = cfg.FileExtension ?? "txt";
        var directory = cfg.Directory ?? "./outbound";
        var fileName = $"{prefix}{now.ToString(dateFormat)}.{ext}";

        var content = writer.BuildContent(effectiveDate, rows);

        await fileSink.WriteAsync(directory, fileName, content, ct);

        // Human-readable Excel companion (same fields, friendly headers) written next to the .txt so
        // non-IT users can inspect what was sent that month.
        var excelFileName = $"{prefix}{now.ToString(dateFormat)}.xlsx";
        var excelBytes = excelWriter.Build(effectiveDate, rows);
        await fileSink.WriteAsync(directory, excelFileName, excelBytes, ct);

        logger.LogInformation(
            "{Tag} Exported {Count} record(s) to {File} (+ {ExcelFile}) in {Dir}",
            JobTag, rows.Count, fileName, excelFileName, directory);
    }
}
