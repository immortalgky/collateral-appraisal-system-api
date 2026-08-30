using Collateral.Contracts.FileInterface;
using Hangfire;
using Integration.Contracts.FileInterface;
using Integration.Contracts.FileSink;
using Integration.FileInterface.Format.RegulatoryExport;
using Integration.Infrastructure.FileSink;
using Microsoft.Extensions.DependencyInjection;
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
/// <b>They do not share a destination.</b> The <c>.txt</c> goes wherever the default outbound sink
/// points, which is AS400's SFTP drop in UAT and production. The workbook has no reader there — the
/// Risk team opens it off a Windows file share — so it takes its directory from
/// <c>REGULATORY_XLSX</c> and always writes through the filesystem adapter. See
/// <see cref="WriteExcelAsync"/>.
///
/// No sent-ledger: every run is a full re-extract.
/// </summary>
public class RegulatoryExportJob(
    IRegulatoryExportQuery query,
    RegulatoryFileWriter writer,
    RegulatoryExcelWriter excelWriter,
    IOutboundFileSink fileSink,
    [FromKeyedServices(OutboundFileSinkKeys.FileSystem)] IOutboundFileSink shareSink,
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

        var excelBytes = excelWriter.Build(effectiveDate, rows);
        var (excelFileName, excelDirectory) = await WriteExcelAsync(excelBytes, now, prefix, dateFormat, directory, ct);

        // Both directories are named so an operator can see from the log alone whether this
        // environment's REGULATORY_XLSX row has been pointed at the share yet.
        logger.LogInformation(
            "{Tag} Exported {Count} record(s): {File} to {Dir}, {ExcelFile} to {ExcelDir}",
            JobTag, rows.Count, fileName, directory, excelFileName, excelDirectory);
    }

    /// <summary>
    /// Writes the workbook to its own destination and returns the name and directory used.
    ///
    /// With an active <c>REGULATORY_XLSX</c> row the file goes to that row's directory through
    /// <c>shareSink</c> — the filesystem adapter — because the destination is a UNC share the
    /// app-pool account writes to directly. Routing it through the default sink instead would hand a
    /// <c>\\host\share</c> path to SFTP, which would try to create that path on the remote host.
    ///
    /// With no row, or the row deactivated, it falls back to exactly the previous behaviour: the
    /// default sink, beside the <c>.txt</c>.
    ///
    /// A failure here is logged and swallowed on purpose. The regulatory obligation is the
    /// <c>.txt</c>, and it has already been written by the time this runs; an unreachable share or a
    /// revoked permission should not mark the run Failed and suggest the regulator's file never went
    /// out. The Error entry is what to alert on.
    /// </summary>
    private async Task<(string FileName, string Directory)> WriteExcelAsync(
        byte[] excelBytes,
        DateTime now,
        string txtPrefix,
        string txtDateFormat,
        string txtDirectory,
        CancellationToken ct)
    {
        var xlsxCfg = await configProvider.GetAsync(FileInterfaceCodes.RegulatoryExcel, ct);

        if (xlsxCfg?.Directory is null)
        {
            var fallbackName = $"{txtPrefix}{now.ToString(txtDateFormat)}.xlsx";
            await fileSink.WriteAsync(txtDirectory, fallbackName, excelBytes, ct);
            return (fallbackName, txtDirectory);
        }

        var fileName =
            $"{xlsxCfg.FileNamePrefix ?? txtPrefix}{now.ToString(xlsxCfg.FileNameDateFormat ?? txtDateFormat)}.{xlsxCfg.FileExtension ?? "xlsx"}";

        try
        {
            await shareSink.WriteAsync(xlsxCfg.Directory, fileName, excelBytes, ct);
        }
        // The filter keeps a shutdown from being reported as a share problem: a cancelled token is
        // the host stopping, not a destination that cannot be written to, and it should propagate.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "{Tag} Could not write the Excel companion {File} to {Dir}. The fixed-width file for "
                + "AS400 was written successfully and is unaffected; only the readable copy is missing.",
                JobTag, fileName, xlsxCfg.Directory);
        }

        return (fileName, xlsxCfg.Directory);
    }
}
