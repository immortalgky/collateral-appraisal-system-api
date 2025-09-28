using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Shared.Messaging.OutboxPatterns.Services;

namespace Shared.Messaging.OutboxPatterns.Jobs;

public class OutboxProcessorJob<TDbContext> : IJob
    where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorJob<TDbContext>> _logger;

    public OutboxProcessorJob(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessorJob<TDbContext>> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("OutboxHostedService<{DbContextType}> job started", typeof(TDbContext).Name);
        
        try
        {
            using var scope = _scopeFactory.CreateScope();

            var inboxService = scope.ServiceProvider.GetKeyedService<IOutboxService>(typeof(TDbContext).Name);

            if (inboxService == null)
            {
                _logger.LogWarning("IOutboxService for {DbContextType} not found", typeof(TDbContext).Name);
                return;
            }

            var messages = await inboxService.PublishEvent(context.CancellationToken);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in outbox processing job for {DbContextType}", typeof(TDbContext).Name);
            throw;
        }
    }
}
//TODO : Best Practice For Query Messages When It Have Too Much Messages (100,000) Waiting for review***
//TODO : Worker Is Working Single Thread ? Waiting for review***
//TODO : Event Type Invalid For Integration Events !!! Waiting for review***