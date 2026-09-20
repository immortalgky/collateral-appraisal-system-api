using Document.Configurations;
using Document.Services;
using Microsoft.Extensions.Options;

namespace Document.Scheduling;

/// <summary>
/// Removes the staging folders of uploads that were never finished. An upload abandoned at 90%
/// holds its bytes on the share until this runs, and at a gigabyte apiece that is worth sweeping
/// on a schedule rather than waiting for the next upload to trigger a tidy-up.
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

        logger.LogInformation(
            "Chunked upload cleanup finished: {Removed} folder(s) older than {Hours}h removed",
            removed, options.Value.StagingRetentionHours);

        return Task.CompletedTask;
    }
}
