namespace Shared.Messaging.OutboxPatterns.Extensions;

public static class QuartzJobExtensions
{
    public static IServiceCollection AddInboxJobs<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        // Add Quartz
        services.AddQuartz(q =>
        {
            var dbContextName = typeof(TDbContext).Name;

            // Inbox cleanup job only
            var inboxJobKey = new JobKey($"inbox-cleanup-{dbContextName}");
            q.AddJob<InboxCleanupJob<TDbContext>>(opts => opts.WithIdentity(inboxJobKey));
            q.AddTrigger(opts => opts
                .ForJob(inboxJobKey)
                .WithIdentity($"inbox-cleanup-trigger-{dbContextName}")
                .WithCronSchedule(configuration.GetValue<string>("Jobs:InboxCleanup:CronExpression", "0/5 * * * * ?")));
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        return services;
    }

    public static IServiceCollection AddOutboxJobs<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        // Add Quartz
        services.AddQuartz(q =>
        {
            var dbContextName = typeof(TDbContext).Name;

            // Outbox processor job only
            var outboxJobKey = new JobKey($"outbox-processor-{dbContextName}");
            q.AddJob<OutboxProcessorJob<TDbContext>>(opts => opts.WithIdentity(outboxJobKey));
            q.AddTrigger(opts => opts
                .ForJob(outboxJobKey)
                .WithIdentity($"outbox-processor-trigger-{dbContextName}")
                .WithCronSchedule(configuration.GetValue<string>("Jobs:OutboxProcessor:CronExpression", "0/30 * * * * ?")));
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        return services;
    }
}