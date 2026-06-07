using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Reporting.Data;
using Reporting.Infrastructure.BrowserPool;
using Shared.Configurations;
using Shared.Time;

namespace Reporting.Application.Services;

/// <summary>
/// Hangfire ad-hoc job: generates a PDF for one report request and writes it to the shared
/// artifact store, then pushes a SignalR notification to the requesting user.
///
/// Idempotency: if the job row is already Completed (e.g. Hangfire retry after a transient
/// failure that occurred AFTER the file was written), this method returns immediately without
/// regenerating the PDF.
///
/// Multi-node note: the file is written to the path determined by FileStorageConfiguration.
/// Local mode writes to the node-local WebRootPath and is therefore single-node only — any
/// download request that lands on a different node will return 410 Gone.
/// Nas mode writes to a shared network path (NasBasePath) visible to all nodes; this is the
/// required configuration for multi-node (N&gt;1) deployments such as the N=2 IIS setup behind
/// the F5 load balancer.
/// </summary>
public class ReportGenerationJob(
    ReportingDbContext dbContext,
    ReportGenerationService reportGenerationService,
    IIntegrationEventOutbox outbox,
    IWebHostEnvironment webHostEnvironment,
    IOptions<FileStorageConfiguration> fileStorageOptions,
    IOptions<ReportingConfiguration> reportingOptions,
    IDateTimeProvider dateTimeProvider,
    ILogger<ReportGenerationJob> logger)
{
    private readonly FileStorageConfiguration _storage = fileStorageOptions.Value;
    private readonly ReportingConfiguration _reporting = reportingOptions.Value;

    public async Task RunAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await dbContext.ReportJobs.FindAsync([jobId], ct);
        if (job is null)
        {
            logger.LogWarning("[REPORT-JOB] Job {JobId} not found — skipping", jobId);
            return;
        }

        // Idempotency: Hangfire may retry after a transient failure. If the PDF was already
        // written and the row marked Completed, do not regenerate.
        if (job.Status == ReportJobStatus.Completed)
        {
            logger.LogInformation("[REPORT-JOB] Job {JobId} already completed — skipping retry", jobId);
            return;
        }

        var now = dateTimeProvider.ApplicationNow;
        job.MarkRunning(now);
        await dbContext.SaveChangesAsync(ct);

        var sw = Stopwatch.StartNew();
        try
        {
            // Generate PDF bytes via the same pipeline used by the synchronous endpoint.
            var bytes = await reportGenerationService.GenerateAsync(
                job.ReportTypeKey, job.EntityId, ct);

            // Determine storage base — same logic as DocumentService.GetStorageBasePath():
            //   NAS mode (shared network share) → NasBasePath (required for multi-node deployments).
            //   Local mode (node-local wwwroot) → WebRootPath (single-node only).
            var storageBase = _storage.Mode == StorageMode.Nas
                              && !string.IsNullOrEmpty(_storage.NasBasePath)
                ? _storage.NasBasePath
                : webHostEnvironment.WebRootPath;

            var directory = Path.Combine(
                storageBase,
                _storage.RootPath.TrimStart('/'),
                _reporting.ReportsSubfolder);

            Directory.CreateDirectory(directory);

            var fileName = $"{job.ReportTypeKey}.pdf";
            var filePath = Path.Combine(directory, $"{jobId}.pdf");

            await File.WriteAllBytesAsync(filePath, bytes, ct);
            sw.Stop();

            now = dateTimeProvider.ApplicationNow;
            job.MarkCompleted(now, filePath, fileName, bytes.LongLength, (int)sw.ElapsedMilliseconds);

            // Enqueue the durable completion event into the transactional outbox BEFORE saving, so
            // the DispatchDomainEventInterceptor drains it into THIS SaveChanges — the ReportJobs
            // 'Completed' row and the outbox row commit atomically (exactly-once: no lost notice if
            // the process dies post-commit; the delivery service publishes it; the Notification
            // consumer persists a recoverable bell entry + realtime ReceiveNotification).
            outbox.Publish(new ReportGenerationCompletedIntegrationEvent
            {
                JobId = jobId,
                ReportTypeKey = job.ReportTypeKey,
                FileName = fileName,
                RequestedByCode = job.RequestedBy,
            }, correlationId: jobId.ToString());

            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "[REPORT-JOB] Job {JobId} completed in {Ms}ms ({Bytes} bytes) → {FilePath}",
                jobId, sw.ElapsedMilliseconds, bytes.Length, filePath);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "[REPORT-JOB] Job {JobId} failed", jobId);

            // Mark the job row Failed AND enqueue the failure event into the same transaction, so
            // the Failed status and the outbox row commit atomically. Use the already-clamped
            // job.ErrorMessage (MarkFailed truncates to 2000) so the event matches the persisted
            // value and its own contract. A separate try/catch keeps a transient DB error from
            // masking the original rendering/IO exception in Hangfire's failure record.
            try
            {
                now = dateTimeProvider.ApplicationNow;
                job.MarkFailed(now, ex.Message);

                outbox.Publish(new ReportGenerationFailedIntegrationEvent
                {
                    JobId = jobId,
                    ReportTypeKey = job.ReportTypeKey,
                    Error = job.ErrorMessage ?? ex.Message,
                    RequestedByCode = job.RequestedBy,
                }, correlationId: jobId.ToString());

                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception dbEx)
            {
                logger.LogError(dbEx,
                    "[REPORT-JOB] Failed to persist Failed status / enqueue failure event for job {JobId}", jobId);
            }

            // Rethrow so Hangfire records the job as failed and can retry/alert.
            throw;
        }
    }
}
