using System.Diagnostics;
using Collateral.Contracts.FileInterface;
using Hangfire;
using Integration.Contracts.FileInterface;
using Integration.Contracts.FileSink;
using Integration.FileInterface.Format.RegulatoryExport;
using Integration.Infrastructure.FileInterface;
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
/// <b>They do not share a destination, and either can be turned off alone.</b> The <c>.txt</c> goes
/// wherever the default outbound sink points, which is AS400's SFTP drop in UAT and production. The
/// workbook has no reader there — the Risk team opens it off a Windows file share — so it takes its
/// directory from <c>REGULATORY_XLSX</c> and always writes through the filesystem adapter.
/// Deactivating that row stops the workbook and leaves the regulator's file untouched; deactivating
/// <c>REGULATORY</c> stops the run entirely. See <see cref="WriteExcelAsync"/>.
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

    /// <summary>
    /// Every step logs when it finishes and how long it took, so a run that produced no file can be
    /// read off the log alone. The one that matters is the row set: it is the only step that has ever
    /// been slow, and when it overran the command timeout in production the log said "Starting" and
    /// then nothing at all, which left no way to tell a stuck query apart from an unreachable SFTP
    /// host. A step that started and did not finish is now the step that failed.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var total = Stopwatch.StartNew();
        var step = new Stopwatch();
        logger.LogInformation("{Tag} Starting monthly regulatory export", JobTag);

        var cfg = await configProvider.GetAsync(FileInterfaceCodes.Regulatory, ct);
        if (cfg is null || !cfg.IsActive)
        {
            logger.LogWarning(
                "{Tag} No active config row for '{Code}'; skipping", JobTag, FileInterfaceCodes.Regulatory);
            return;
        }

        logger.LogInformation(
            "{Tag} Reading the row set — this runs collateral.sp_RegulatoryExport and is the long step",
            JobTag);

        IReadOnlyList<RegulatoryExportRow> rows;
        step.Restart();
        try
        {
            rows = await query.GetRowsAsync(ct);
        }
        // Logged and rethrown, not handled: Hangfire still has to see the run fail. What the entry
        // adds is the elapsed time, which tells a command timeout (600s) apart from a connection that
        // was refused in a second, without opening the dashboard.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(
                ex, "{Tag} Reading the row set failed after {Seconds:n1}s; no file was written",
                JobTag, step.Elapsed.TotalSeconds);
            throw;
        }

        logger.LogInformation(
            "{Tag} Row set read: {Count} row(s) in {Seconds:n1}s", JobTag, rows.Count, step.Elapsed.TotalSeconds);

        if (rows.Count == 0)
        {
            logger.LogInformation("{Tag} No reportable collateral found; nothing to send", JobTag);
            return;
        }

        // Taken AFTER the query on purpose: the file is named for the moment it is written, and the
        // read above can run for minutes.
        var now = dateTimeProvider.ApplicationNow;
        var effectiveDate = DateOnly.FromDateTime(now);
        var prefix = cfg.FileNamePrefix ?? "REGULATORY_";
        var dateFormat = cfg.FileNameDateFormat ?? "yyyyMMdd";
        var ext = cfg.FileExtension ?? "txt";
        var directory = cfg.Directory ?? "./outbound";
        var fileName = OutboundFileName.Build(prefix, dateFormat, ext, now);

        step.Restart();
        var content = writer.BuildContent(effectiveDate, rows);
        logger.LogInformation(
            "{Tag} Fixed-width content built: {Lines} line(s), {Chars} character(s), in {Seconds:n1}s",
            JobTag, rows.Count + 2, content.Length, step.Elapsed.TotalSeconds);

        // The sink name is here because the destination alone does not say how the file got there:
        // the same directory string goes to SFTP or to the local filesystem depending on
        // FileTransfer:Outbound:FileSource, which is resolved once at startup and is invisible at
        // this point otherwise.
        step.Restart();
        await fileSink.WriteAsync(directory, fileName, content, ct);
        logger.LogInformation(
            "{Tag} Exported {Count} record(s) to {File} in {Dir} via {Sink} in {Seconds:n1}s",
            JobTag, rows.Count, fileName, directory, fileSink.GetType().Name, step.Elapsed.TotalSeconds);

        // The workbook has its own destination and logs its own outcome — it may not be written at
        // all. Deliberately after the summary above, so the regulator's file is reported as sent
        // whatever happens to the companion.
        step.Restart();
        var excelBytes = excelWriter.Build(effectiveDate, rows);
        logger.LogInformation(
            "{Tag} Workbook built: {Bytes} byte(s) in {Seconds:n1}s",
            JobTag, excelBytes.Length, step.Elapsed.TotalSeconds);

        await WriteExcelAsync(excelBytes, now, prefix, dateFormat, ct);

        logger.LogInformation("{Tag} Finished in {Seconds:n1}s", JobTag, total.Elapsed.TotalSeconds);
    }

    /// <summary>
    /// Writes the workbook to the destination on the <c>REGULATORY_XLSX</c> row, through
    /// <c>shareSink</c> — the filesystem adapter — because that destination is a UNC share the
    /// app-pool account writes to directly. Routing it through the default sink instead would hand a
    /// <c>\\host\share</c> path to SFTP, which would try to create that path on the remote host.
    ///
    /// <b>No row, a deactivated row, or a row with no directory means the workbook is not written
    /// at all.</b> This mirrors how <see cref="ExecuteAsync"/> treats the <c>REGULATORY</c> row a few
    /// lines above: absent or inactive is an instruction not to produce the file, not a cue to guess
    /// a destination. Writing it somewhere else would make <c>IsActive = 0</c> unable to express
    /// "stop producing this", which is the only thing an operator would set it for.
    ///
    /// Nothing is lost by having no fallback here. The migration seeds the row active and pointed at
    /// the same directory as the <c>.txt</c>, so a database that has run it always has a destination
    /// even if the per-environment UPDATE was never applied. The seeded value is the safety net; a
    /// second one in code would only ever fire on a half-applied deploy, and would cost the kill
    /// switch to buy it.
    ///
    /// A write failure is logged and swallowed on purpose. The regulatory obligation is the
    /// <c>.txt</c>, and it has already been written by the time this runs; an unreachable share or a
    /// revoked permission should not mark the run Failed and suggest the regulator's file never went
    /// out. The Error entry is what to alert on.
    /// </summary>
    private async Task WriteExcelAsync(
        byte[] excelBytes,
        DateTime now,
        string txtPrefix,
        string txtDateFormat,
        CancellationToken ct)
    {
        var xlsxCfg = await configProvider.GetAsync(FileInterfaceCodes.RegulatoryExcel, ct);

        if (xlsxCfg?.Directory is null)
        {
            logger.LogWarning(
                "{Tag} No active '{Code}' config row with a directory; the Excel companion was not "
                + "written. The fixed-width file for AS400 is unaffected.",
                JobTag, FileInterfaceCodes.RegulatoryExcel);
            return;
        }

        // The .txt row supplies the defaults so the workbook keeps its usual name unless the
        // REGULATORY_XLSX row deliberately overrides one of these.
        //
        // That inheritance now carries an empty FileNameDateFormat too, which is what the bank's
        // undated RDTCLSINT4.txt uses. An environment that wants a dated workbook must therefore
        // spell yyyyMMdd out on the REGULATORY_XLSX row rather than leave it NULL — otherwise the
        // workbook silently loses its date along with the .txt.
        var fileName = OutboundFileName.Build(
            xlsxCfg.FileNamePrefix ?? txtPrefix,
            xlsxCfg.FileNameDateFormat ?? txtDateFormat,
            xlsxCfg.FileExtension ?? "xlsx",
            now);

        var step = Stopwatch.StartNew();
        try
        {
            await shareSink.WriteAsync(xlsxCfg.Directory, fileName, excelBytes, ct);

            // Named so an operator can see from the log alone whether this environment's
            // REGULATORY_XLSX row has been pointed at the share yet. The elapsed time is worth having
            // separately from the .txt's: this one crosses a UNC share, so it is the write that can
            // sit for a long time before it fails.
            logger.LogInformation(
                "{Tag} Wrote the Excel companion {ExcelFile} to {ExcelDir} in {Seconds:n1}s",
                JobTag, fileName, xlsxCfg.Directory, step.Elapsed.TotalSeconds);
        }
        // The filter keeps a shutdown from being reported as a share problem: a cancelled token is
        // the host stopping, not a destination that cannot be written to, and it should propagate.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "{Tag} Could not write the Excel companion {File} to {Dir} after {Seconds:n1}s. The "
                + "fixed-width file for AS400 was written successfully and is unaffected; only the "
                + "readable copy is missing.",
                JobTag, fileName, xlsxCfg.Directory, step.Elapsed.TotalSeconds);
        }
    }
}
