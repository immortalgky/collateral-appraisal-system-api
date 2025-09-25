namespace Shared.Messaging.OutboxPatterns.Jobs;

public class InboxCleanupOptions
{
    public const string SectionName = "Jobs:InboxCleanup";
    public bool Enabled { get; set; } = true;
    public int RetentionDays { get; set; } = 7;
    public string CronExpression { get; set; } = "0 1 0 * * ?"; //CRON JOB
}