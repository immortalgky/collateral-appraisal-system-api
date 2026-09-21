using Document.Configurations;
using Document.Services;
using Microsoft.Extensions.Options;

namespace Document.Scheduling;

/// <summary>
/// Removes what unfinished uploads leave on the share — chunk folders and streamed staging files
/// alike. An upload abandoned at 90% holds its bytes until this runs, and at a gigabyte apiece
/// that is worth sweeping on a schedule rather than waiting for the next upload to tidy up.
/// </summary>
internal sealed class ChunkedUploadCleanupJob(
    IChunkedUploadStore store,
    IOptions<ChunkedUploadOptions> options,
    ILogger<ChunkedUploadCleanupJob> logger)
{
    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var retention = TimeSpan.FromHours(options.Value.StagingRetentionHours);

        // Both app nodes run this. Whichever gets there first deletes the folder; the other finds
        // it gone and moves on, which the store treats as an ordinary outcome rather than an error.
        var removed = store.DeleteExpired(retention);

        // The streamed route — the one an outside system posts a whole file to — stages its bytes
        // beside these, and loses them the same way when a request dies mid-flight.
        var streamed = store.DeleteExpiredStreamedFiles(retention);

        logger.LogInformation(
            "Upload staging cleanup finished: {Removed} chunk folder(s) and {Streamed} streamed file(s) older than {Hours}h removed",
            removed, streamed, options.Value.StagingRetentionHours);

        return Task.CompletedTask;
    }
}
