using Document.Data;
using Hangfire;
using Shared.Data.Outbox;
using Shared.Scheduling;

namespace Document.Scheduling;

/// <summary>
/// Recurring jobs owned by the Document module. Seeded into and read from <c>document.JobSchedules</c>
/// by <c>app.UseModuleRecurringJobs&lt;DocumentDbContext&gt;()</c>.
/// </summary>
public static class DocumentRecurringJobs
{
    public static readonly IReadOnlyList<RecurringJobDefinition> All = new[]
    {
        new RecurringJobDefinition("outbox-cleanup-document", "0 2 * * *",
            "Purge processed/dead-letter outbox messages for the Document module (daily at 02:00).",
            (mgr, cron, opt) => mgr.AddOrUpdate<OutboxCleanupJob<DocumentDbContext>>(
                "outbox-cleanup-document", j => j.ExecuteAsync(CancellationToken.None), cron, opt)),

        new RecurringJobDefinition("chunked-upload-cleanup-document", "15 * * * *",
            "Delete staging folders of chunked uploads that were never completed (hourly at :15).",
            (mgr, cron, opt) => mgr.AddOrUpdate<ChunkedUploadCleanupJob>(
                "chunked-upload-cleanup-document", j => j.ExecuteAsync(CancellationToken.None), cron, opt))
    };
}
