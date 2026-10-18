using Collateral.Contracts.FileInterface;
using Hangfire;
using Integration.Contracts.FileInterface;
using Integration.Contracts.FileSink;
using Integration.FileInterface.Format.CollateralResult;
using Integration.Infrastructure.FileInterface;
using Microsoft.Extensions.Logging;
using Shared.Time;

namespace Integration.FileInterface.Jobs.CollateralResult;

/// <summary>
/// Hangfire recurring job, daily at 04:00 (the schedule is owned by the admin job-schedule UI; the code default in
/// IntegrationRecurringJobs only seeds fresh environments). Builds the outbound "Collateral Result" file from
/// unsent approved (A) rows. File name and path come from
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
            logger.LogInformation("{Tag} No unsent rows; nothing to send", JobTag);
            return;
        }

        var now = dateTimeProvider.ApplicationNow;
        var effectiveDate = DateOnly.FromDateTime(now);
        var prefix = cfg.FileNamePrefix ?? "COLLATERAL_RESULT_";
        var dateFormat = cfg.FileNameDateFormat ?? "yyyyMMddHHmmss";
        var ext = cfg.FileExtension ?? "txt";
        var directory = cfg.Directory ?? "./outbound";
        var fileName = OutboundFileName.Build(prefix, dateFormat, ext, now);

        var content = writer.BuildContent(effectiveDate, rows);

        // Write/upload first; only record the ledger on success (at-least-once).
        await fileSink.WriteAsync(directory, fileName, content, ct);

        await ledger.MarkSentAsync(rows, fileName, now, ct);

        logger.LogInformation(
            "{Tag} Sent {Total} record(s) in {File} in {Dir}",
            JobTag, rows.Count, fileName, directory);
    }

    /// <summary>
    /// Builds the file for the same rows, in the same order, as the next run would send, and returns it
    /// without writing a file or touching the ledger. Running the real job to get a file to look at would
    /// mark everything sent.
    ///
    /// It can differ from the delivered file: the header date is the preview day, while the job stamps
    /// its own run date; with 0 rows the preview still returns a header and trailer, but the job sends
    /// nothing; and the preview ignores the config's IsActive.
    /// </summary>
    public async Task<CollateralResultPreview> BuildPreviewAsync(CancellationToken ct = default)
    {
        var rows = await query.GetUnsentRowsAsync(ct);
        var effectiveDate = DateOnly.FromDateTime(dateTimeProvider.ApplicationNow);

        return new CollateralResultPreview(
            RowCount: rows.Count,
            ApprovedCount: rows.Count(r => r.AppraisalStatus == "A"),
            AutoUpdateCount: rows.Count(r => r.AutoUpdate == "Y"),
            RecordLength: CollateralResultFileWriter.RecordLength,
            Content: writer.BuildContent(effectiveDate, rows));
    }
}

/// <param name="AutoUpdateCount">
/// Rows sent with Auto Update 'Y': reappraisals Initiate raised for a COLLATREV book whose candidate
/// carried AS400's own 'Y'. Every other row goes out 'N'.
/// </param>
public record CollateralResultPreview(
    int RowCount,
    int ApprovedCount,
    int AutoUpdateCount,
    int RecordLength,
    string Content);
