using Collateral.Contracts.HostLink;
using Hangfire;
using Integration.Contracts.FileInterface;
using Integration.FileInterface.Format.HostLink;
using Integration.Infrastructure.FileInterface;
using Microsoft.Extensions.Logging;

namespace Integration.FileInterface.Jobs.HostLink;

/// <summary>
/// Hangfire recurring job that ingests the AS400 COLLATLINK file — the feed that maps our appraisal
/// numbers to AS400 collateral ids (CCDCID).
///
/// <b>The file is monthly and is a full replace</b>, not a delta: whatever it contains is the entire
/// set of collateral the bank holds, and anything absent from it is no longer held. Everything about
/// finding, de-duplicating and recording the file is <see cref="InboundFileRunner"/>'s; this class
/// says which file, how to read its date, and what to do with the bytes.
/// </summary>
public class As400HostLinkJob(
    InboundFileRunner runner,
    HostCollateralLinkFileParser parser,
    IHostCollateralLinkIngestor ingestor,
    ILogger<As400HostLinkJob> logger)
{
    private const string LogTag = "[HOST-LINK-AS400]";

    /// <summary>
    /// A run can outlive its schedule when the backlog is large or the host is slow. Two runs writing
    /// HostCollateralLinks at once would interleave their replaces, so the second one waits — and
    /// gives up rather than piling on if the first is still going after five minutes.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public Task ExecuteAsync(CancellationToken cancellationToken = default) =>
        runner.RunAsync(
            new InboundFileInterface(
                Code: FileInterfaceCodes.HostCollateralLink,
                LogTag: LogTag,
                DefaultDirectory: "./hostlink/inbox",
                DefaultFilePattern: "AS400_COLLATLINK_*.txt",
                ParseFileDate: HostCollateralLinkFileParser.ParseFilenameDate,
                IngestAsync: ApplyAsync),
            cancellationToken);

    private async Task<InboundIngestOutcome> ApplyAsync(
        string fileName, byte[] content, DateOnly fileDate, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(content, writable: false);
        var parsed = parser.ParseStream(buffer);

        var result = await ingestor.IngestAsync(fileName, fileDate, parsed, cancellationToken);

        if (result.SkippedAsStale)
        {
            return new InboundIngestOutcome(
                Received: parsed.Records.Count,
                SkippedStale: true,
                StaleReason: "A newer COLLATLINK file has already been applied.");
        }

        logger.LogInformation(
            "{Tag} deactivated={Deactivated} collateral no longer listed by the newest file",
            LogTag, result.Deactivated);

        return new InboundIngestOutcome(
            Received: result.Received,
            Updated: result.Updated,
            Unchanged: result.Unchanged,
            Summary: $"received={result.Received} updated={result.Updated} "
                     + $"unchanged={result.Unchanged} deactivated={result.Deactivated}");
    }
}
