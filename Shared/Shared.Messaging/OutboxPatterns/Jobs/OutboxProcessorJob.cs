namespace Shared.Messaging.OutboxPatterns.Jobs;

public class OutboxProcessorJob<TDbContext> : IJob
    where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorJob<TDbContext>> _logger;

    public OutboxProcessorJob(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessorJob<TDbContext>> logger
        )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("OutboxHostedService<{DbContextType}> job started", typeof(TDbContext).Name);
        
        try
        {
            using var scope = _scopeFactory.CreateScope();

            var inboxService = scope.ServiceProvider.GetKeyedService<IOutboxService>(typeof(TDbContext).Name);

            if (inboxService == null)
            {
                _logger.LogWarning("IOutboxService for {DbContextType} not found", typeof(TDbContext).Name);
                return;
            }

            await inboxService.PublishEvent(context.CancellationToken);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in outbox processing job for {DbContextType}", typeof(TDbContext).Name);
        }
    }
}