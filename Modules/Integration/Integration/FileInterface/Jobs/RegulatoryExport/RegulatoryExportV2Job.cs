using Collateral.Contracts.FileInterface;
using Hangfire;
using Integration.Contracts.FileInterface;
using Integration.Contracts.FileSink;
using Integration.FileInterface.Format.RegulatoryExport;
using Microsoft.Extensions.Logging;
using Shared.Time;

namespace Integration.FileInterface.Jobs.RegulatoryExport;

/// <summary>
/// Version 2 of the monthly regulatory export. Identical to <see cref="RegulatoryExportJob"/> in every
/// respect except where the rows come from: <see cref="IRegulatoryExportV2Query"/> reads the appraisal
/// chain rather than CollateralMaster.
///
/// <b>Why a whole second job rather than a switch inside the first.</b> The two are meant to run
/// against the same data so their files can be compared before v1 is retired, and the choice of which
/// one is live has to be visible and reversible without a deployment. A recurring job is exactly that
/// — <c>integration.JobSchedules.IsEnabled</c> toggles it — whereas a config flag inside one job
/// leaves no trace of which version produced a given file.
///
/// This job ships DISABLED. Enable it to start producing the shadow file; disable
/// <c>regulatory-export</c> once its output has been accepted.
///
/// Both writers are shared unchanged: RegulatoryExportRow carries no CollateralMaster reference, so
/// the 300-char and Excel formatting is identical whichever query filled it.
/// </summary>
public class RegulatoryExportV2Job(
    IRegulatoryExportV2Query query,
    RegulatoryFileWriter writer,
    RegulatoryExcelWriter excelWriter,
    IOutboundFileSink fileSink,
    IFileInterfaceConfigProvider configProvider,
    IDateTimeProvider dateTimeProvider,
    ILogger<RegulatoryExportV2Job> logger)
{
    private const string JobTag = "[REGULATORY-EXPORT-V2]";

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        logger.LogInformation("{Tag} Starting monthly regulatory export (chain-based)", JobTag);

        var cfg = await configProvider.GetAsync(FileInterfaceCodes.RegulatoryV2, ct);
        if (cfg is null || !cfg.IsActive)
        {
            logger.LogWarning(
                "{Tag} No active config row for '{Code}'; skipping", JobTag, FileInterfaceCodes.RegulatoryV2);
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
        var prefix = cfg.FileNamePrefix ?? "REGULATORY_V2_";
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
