using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Shared.Messaging.OutboxPatterns.Jobs;

namespace Shared.Messaging.OutboxPatterns.Extensions;

public static class QuartzJobExtensions
{
    public static IServiceCollection AddOutboxJobs<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        // Configure options
        services.Configure<InboxCleanupOptions>(
            configuration.GetSection(InboxCleanupOptions.SectionName));
        // services.Configure<OutboxProcessorOptions>(
        //     configuration.GetSection(OutboxProcessorOptions.SectionName));

        // Add Quartz
        services.AddQuartz(q =>
        {
            // Use unique job keys per DbContext
            var dbContextName = typeof(TDbContext).Name;
            
            // Inbox cleanup job - daily at 00:01
            var inboxJobKey = new JobKey($"inbox-cleanup-{dbContextName}");
            q.AddJob<InboxCleanupJob<TDbContext>>(opts => opts.WithIdentity(inboxJobKey));
            q.AddTrigger(opts => opts
                .ForJob(inboxJobKey)
                .WithIdentity($"inbox-cleanup-trigger-{dbContextName}")
                .WithCronSchedule(configuration.GetValue<string>($"{InboxCleanupOptions.SectionName}:CronExpression", "0 1 0 * * ?")));

            // Outbox processor job - every 30 seconds
            // var outboxJobKey = new JobKey($"outbox-processor-{dbContextName}");
            // q.AddJob<OutboxProcessorJob<TDbContext>>(opts => opts.WithIdentity(outboxJobKey));
            // q.AddTrigger(opts => opts
            //     .ForJob(outboxJobKey)
            //     .WithIdentity($"outbox-processor-trigger-{dbContextName}")
            //     .WithCronSchedule(configuration.GetValue<string>($"{OutboxProcessorOptions.SectionName}:CronExpression", "0/30 * * * * ?")));
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        return services;
    }
}