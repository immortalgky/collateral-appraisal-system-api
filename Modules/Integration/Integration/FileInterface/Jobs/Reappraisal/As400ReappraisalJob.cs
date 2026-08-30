using Collateral.Contracts.Reappraisal;
using Hangfire;
using Integration.Contracts.FileInterface;
using Integration.FileInterface.Format.Reappraisal;
using Integration.Infrastructure.FileInterface;

namespace Integration.FileInterface.Jobs.Reappraisal;

/// <summary>
/// Hangfire recurring job that ingests the AS400 COLLATREV file — the list of appraisals the bank
/// wants reviewed.
///
/// Everything about finding, de-duplicating and recording the file is <see cref="InboundFileRunner"/>'s;
/// this class says which file, how to read its date, and what to do with the bytes.
/// </summary>
public class As400ReappraisalJob(
    InboundFileRunner runner,
    CollatrevFileParser parser,
    IReappraisalIngestor ingestor)
{
    /// <summary>
    /// Candidates are keyed by (file date, collateral id, survey number); two runs importing the same
    /// file at once would race on that unique index. The second run waits, then gives up rather than
    /// piling on.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public Task ExecuteAsync(CancellationToken cancellationToken = default) =>
        runner.RunAsync(
            new InboundFileInterface(
                Code: FileInterfaceCodes.Reappraisal,
                LogTag: "[REAPPRAISAL-AS400]",
                DefaultDirectory: "./reappraisal/inbox",
                DefaultFilePattern: "AS400_COLLATREV_*.txt",
                ParseFileDate: CollatrevFileParser.ParseFilenameDate,
                IngestAsync: ApplyAsync),
            cancellationToken);

    private async Task<InboundIngestOutcome> ApplyAsync(
        string fileName, byte[] content, DateOnly fileDate, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(content, writable: false);
        var parsed = parser.ParseStream(buffer);

        await ingestor.IngestAsync(fileName, fileDate, parsed, cancellationToken);

        return new InboundIngestOutcome(Received: parsed.Details.Count);
    }
}
