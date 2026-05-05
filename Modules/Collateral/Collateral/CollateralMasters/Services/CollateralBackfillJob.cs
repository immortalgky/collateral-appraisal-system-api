using System.Collections.Concurrent;
using Appraisal.Application.Features.Appraisals.GetCompletedAppraisalIdsForBackfill;
using Collateral.CollateralMasters.Exceptions;
using Collateral.CollateralMasters.Models;
using Collateral.Data;

namespace Collateral.CollateralMasters.Services;

// ---------------------------------------------------------------------------
// Job status tracked in-memory per run
// ---------------------------------------------------------------------------

public enum BackfillJobState { Started, InProgress, Completed }

public record BackfillJobStatus(
    Guid JobId,
    DateTime StartedAt,
    DateTime? CompletedAt,
    BackfillJobState State,
    int Processed,
    int Skipped,
    int Errors);

// ---------------------------------------------------------------------------
// Job class
// ---------------------------------------------------------------------------

/// <summary>
/// One-shot background backfill job. Does NOT start automatically with the app.
/// Triggered via <see cref="StartAsync"/> from the admin endpoint (fire-and-forget).
///
/// Algorithm:
///   - Pages through completed appraisals oldest-first (100 per page).
///   - Per appraisal: calls <see cref="ICollateralMasterUpsertService.ProcessAppraisalAsync"/>.
///   - Writes a <see cref="CollateralBackfillReport"/> row per appraisal.
///   - A fresh <see cref="IServiceScope"/> is used per appraisal so DbContext does not accumulate state.
///   - Idempotent: unique (AppraisalId, PropertyId) index on engagements + detail-table unique indexes.
///   - Job tracking is in-memory only (ConcurrentDictionary); BackfillReport rows are the durable log.
/// </summary>
public class CollateralBackfillJob(
    IServiceScopeFactory scopeFactory,
    ILogger<CollateralBackfillJob> logger)
{
    private const int PageSize = 100;

    // In-memory job state — simple enough for v1 (no persistence required).
    private readonly ConcurrentDictionary<Guid, BackfillJobStatus> _jobs = new();

    /// <summary>
    /// Returns current status for a running or completed job.
    /// Returns null if the jobId is unknown.
    /// </summary>
    public BackfillJobStatus? GetJobStatus(Guid jobId)
        => _jobs.TryGetValue(jobId, out var status) ? status : null;

    /// <summary>
    /// Starts the backfill in the background. Returns the JobId immediately.
    /// If a job is already InProgress the caller may still start a new one
    /// (idempotency on the data layer protects against duplicates).
    /// </summary>
    public Guid StartAsync(CancellationToken ct = default)
    {
        var jobId = Guid.CreateVersion7();
        var status = new BackfillJobStatus(jobId, DateTime.UtcNow, null, BackfillJobState.Started, 0, 0, 0);
        _jobs[jobId] = status;

        // Fire-and-forget — not awaited by caller
        _ = Task.Run(() => RunAsync(jobId, ct), ct);

        return jobId;
    }

    // -----------------------------------------------------------------------
    // Core algorithm
    // -----------------------------------------------------------------------

    private async Task RunAsync(Guid jobId, CancellationToken ct)
    {
        UpdateStatus(jobId, BackfillJobState.InProgress);
        logger.LogInformation("CollateralBackfillJob {JobId}: started", jobId);

        int processed = 0, skipped = 0, errors = 0;
        int page = 1;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Fetch next batch of completed appraisal IDs
                IReadOnlyList<Guid> batch;
                using (var batchScope = scopeFactory.CreateScope())
                {
                    var mediator = batchScope.ServiceProvider.GetRequiredService<ISender>();
                    batch = await mediator.Send(
                        new GetCompletedAppraisalIdsForBackfillQuery(page, PageSize), ct);
                }

                if (batch.Count == 0)
                    break;

                foreach (var appraisalId in batch)
                {
                    if (ct.IsCancellationRequested) break;
                    var result = await ProcessOneScopedAsync(appraisalId, ct);
                    switch (result)
                    {
                        case "Processed": processed++; break;
                        case "SkippedMissingKey": skipped++; break;
                        default: errors++; break;
                    }

                    // Update counters in-memory after each item
                    UpdateCounters(jobId, processed, skipped, errors);
                }

                if (batch.Count < PageSize)
                    break;

                page++;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CollateralBackfillJob {JobId}: unexpected error in outer loop", jobId);
        }
        finally
        {
            _jobs[jobId] = _jobs[jobId] with
            {
                State = BackfillJobState.Completed,
                CompletedAt = DateTime.UtcNow,
                Processed = processed,
                Skipped = skipped,
                Errors = errors
            };

            logger.LogInformation(
                "CollateralBackfillJob {JobId}: finished. Processed={Processed} Skipped={Skipped} Errors={Errors}",
                jobId, processed, skipped, errors);
        }
    }

    /// <summary>
    /// Processes a single appraisal in its own DI scope.
    /// Returns the status string used to write the BackfillReport row.
    /// Never throws — errors are caught and recorded.
    /// </summary>
    private async Task<string> ProcessOneScopedAsync(Guid appraisalId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var upsertService = scope.ServiceProvider.GetRequiredService<ICollateralMasterUpsertService>();
        var db = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();

        string status;
        string? message = null;

        try
        {
            await upsertService.ProcessAppraisalAsync(appraisalId, ct);
            status = "Processed";
            logger.LogDebug("CollateralBackfillJob: Processed AppraisalId={AppraisalId}", appraisalId);
        }
        catch (MissingIdentityKeyException ex)
        {
            status = "SkippedMissingKey";
            message = ex.Message;
            logger.LogWarning(
                "CollateralBackfillJob: SkippedMissingKey AppraisalId={AppraisalId} Reason={Reason}",
                appraisalId, ex.Message);
        }
        catch (Exception ex)
        {
            status = "Error";
            var full = ex.ToString();
            message = full.Length > 1000 ? full[..1000] : full;
            logger.LogError(ex, "CollateralBackfillJob: Error processing AppraisalId={AppraisalId}", appraisalId);
        }

        // Write the report row — use the same scope's DbContext
        var report = new CollateralBackfillReport(appraisalId, status, message);
        db.CollateralBackfillReports.Add(report);
        await db.SaveChangesAsync(ct);

        return status;
    }

    // -----------------------------------------------------------------------
    // In-memory helpers
    // -----------------------------------------------------------------------

    private void UpdateStatus(Guid jobId, BackfillJobState state)
    {
        if (_jobs.TryGetValue(jobId, out var current))
            _jobs[jobId] = current with { State = state };
    }

    private void UpdateCounters(Guid jobId, int processed, int skipped, int errors)
    {
        if (_jobs.TryGetValue(jobId, out var current))
            _jobs[jobId] = current with { Processed = processed, Skipped = skipped, Errors = errors };
    }
}
