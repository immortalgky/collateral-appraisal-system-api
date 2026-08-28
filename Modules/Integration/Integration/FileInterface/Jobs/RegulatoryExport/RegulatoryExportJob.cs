using Collateral.Contracts.FileInterface;
using Hangfire;
using Integration.Contracts.FileInterface;
using Integration.Contracts.FileSink;
using Integration.FileInterface.Format.RegulatoryExport;
using Microsoft.Extensions.Logging;
using Shared.Time;

namespace Integration.FileInterface.Jobs.RegulatoryExport;

/// <summary>
/// Hangfire monthly recurring job. Builds the outbound "CAS-AS400-Regulatory" file — one Detail record
/// per collateral the bank holds, carrying that collateral's first appraisal. File name and path come
/// from <c>integration.FileInterfaceConfigs</c> (<c>REGULATORY</c>); the schedule comes from
/// <c>integration.JobSchedules</c> (<c>regulatory-export</c>).
///
/// Two files are written on every run: the fixed-width 300-char <c>.txt</c> that AS400 consumes, and
/// an <c>.xlsx</c> companion with the same rows in a form people can read. Both are built from the
/// same <see cref="RegulatoryExportRow"/> list — but each writer keeps its OWN field map, so a field
/// that moves in one must move in the other in the same commit.
///
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
            logger.LogWarning(
                "{Tag} No active config row for '{Code}'; skipping", JobTag, FileInterfaceCodes.Regulatory);
            return;
        }

        var rows = await query.GetRowsAsync(ct);
        if (rows.Count == 0)
        {
            logger.LogInformation("{Tag} No reportable collateral found; nothing to send", JobTag);
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

        var excelFileName = $"{prefix}{now.ToString(dateFormat)}.xlsx";
        var excelBytes = excelWriter.Build(effectiveDate, rows);
        await fileSink.WriteAsync(directory, excelFileName, excelBytes, ct);

        logger.LogInformation(
            "{Tag} Exported {Count} record(s) to {File} (+ {ExcelFile}) in {Dir}",
            JobTag, rows.Count, fileName, excelFileName, directory);
    }
}
