namespace Shared.Messaging.OutboxPatterns.Jobs;

public class InboxCleanupJob<TDbContext> : IJob
    where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxCleanupJob<TDbContext>> _logger;
    public InboxCleanupJob(
        IServiceProvider serviceProvider,
        ILogger<InboxCleanupJob<TDbContext>> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        var job = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var scope = _serviceProvider.CreateScope();

            var inboxService = scope.ServiceProvider.GetKeyedService<IInboxService>(typeof(TDbContext).Name);

            if (inboxService is null) { _logger.LogWarning("Inbox service not found for {DbContext}", typeof(TDbContext).Name); return; }

            await inboxService.ClearTimeOutMessage();

            job.Stop();

            _logger.LogInformation("Inbox cleanup completed in {Duration}ms", job.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            job.Stop();

            _logger.LogError(ex, "Error during inbox cleanup job after {Duration}ms",job.ElapsedMilliseconds);

            return;
        }
    }

    
}