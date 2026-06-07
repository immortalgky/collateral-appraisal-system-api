using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reporting.Data;
using Reporting.Infrastructure.BrowserPool;
using Shared.Time;

namespace Reporting.Application.Services;

/// <summary>
/// Hangfire recurring job (daily at 03:00 local): deletes completed/failed report job rows
/// and their on-disk PDF artifacts that are older than <see cref="ReportingConfiguration.ArtifactRetentionDays"/>.
///
/// Toleration: if the on-disk file is already missing (cleaned up externally or written to a
/// different node in Local mode), the row is still deleted without error.
/// </summary>
public class ReportArtifactCleanupJob(
    ReportingDbContext dbContext,
    IOptions<ReportingConfiguration> reportingOptions,
    IDateTimeProvider dateTimeProvider,
    ILogger<ReportArtifactCleanupJob> logger)
{
    private readonly ReportingConfiguration _reporting = reportingOptions.Value;

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        logger.LogInformation("[REPORT-CLEANUP] Starting artifact cleanup");

        try
        {
            await RunCleanupAsync(ct);
        }
        catch (Exception ex)
        {
            // Re-throw so Hangfire records the run as failed.
            logger.LogError(ex, "[REPORT-CLEANUP] Cleanup failed");
            throw;
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        var cutoff = dateTimeProvider.ApplicationNow
            .AddDays(-_reporting.ArtifactRetentionDays);

        // Only terminal states are subject to cleanup; Pending/Running jobs are active.
        // Age is measured from when the artifact was written (CompletedAt), falling back to
        // RequestedAt for Failed rows that never produced a file — so a job that sat queued or
        // retried for days isn't purged based on when it was merely requested.
        var expiredJobs = await dbContext.ReportJobs
            .Where(j =>
                (j.Status == ReportJobStatus.Completed || j.Status == ReportJobStatus.Failed)
                && (j.CompletedAt ?? j.RequestedAt) <= cutoff)
            .ToListAsync(ct);

        if (expiredJobs.Count == 0)
        {
            logger.LogInformation("[REPORT-CLEANUP] No expired jobs found");
            return;
        }

        var deletedFiles = 0;
        foreach (var job in expiredJobs)
        {
            if (!string.IsNullOrEmpty(job.StoragePath) && File.Exists(job.StoragePath))
            {
                try
                {
                    File.Delete(job.StoragePath);
                    deletedFiles++;
                }
                catch (Exception ex)
                {
                    // Log and continue — one missing file should not abort the entire cleanup.
                    logger.LogWarning(ex,
                        "[REPORT-CLEANUP] Failed to delete artifact for job {JobId} at {Path}",
                        job.Id, job.StoragePath);
                }
            }
        }

        dbContext.ReportJobs.RemoveRange(expiredJobs);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "[REPORT-CLEANUP] Removed {JobCount} expired job rows, deleted {FileCount} artifact files (cutoff: {Cutoff:yyyy-MM-dd})",
            expiredJobs.Count, deletedFiles, cutoff);
    }
}
