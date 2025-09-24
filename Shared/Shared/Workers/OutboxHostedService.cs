using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.OutboxPatterns.Services;

namespace Shared.Workers;

public class OutboxHostedService<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxHostedService<TDbContext>> _logger;
    private readonly TimeSpan _slowInterval;
    private readonly TimeSpan _fastInterval;
    private const int MaxFastModeCycles = 10;

    public OutboxHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxHostedService<TDbContext>> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _fastInterval = TimeSpan.FromSeconds(configuration.GetValue<int>("OutboxConfigurations:TimeSpanFast"));
        _slowInterval = TimeSpan.FromSeconds(configuration.GetValue<int>("OutboxConfigurations:TimeSpanSlow"));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var isFastMode = false;
        var emptyMessageCycles = 0;

        _logger.LogInformation("OutboxHostedService<{DbContextType}> started", typeof(TDbContext).Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var contextKey = typeof(TDbContext).Name;
                var service = scope.ServiceProvider.GetRequiredKeyedService<IOutboxService>(contextKey);
                
                var messagesProcessed = await service.PublishEvent(stoppingToken);

                if (messagesProcessed > 0)
                {
                    // Found messages - switch to fast mode
                    isFastMode = true;
                    emptyMessageCycles = 0;
                    _logger.LogDebug("Processed {Count} messages, switching to fast mode", messagesProcessed);
                }
                else if (isFastMode)
                {
                    // No messages in fast mode - count empty cycles
                    emptyMessageCycles++;
                    if (emptyMessageCycles >= MaxFastModeCycles)
                    {
                        isFastMode = false;
                        emptyMessageCycles = 0;
                        _logger.LogDebug("No messages for {Cycles} cycles, switching to slow mode", MaxFastModeCycles);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox processing for {DbContextType}", typeof(TDbContext).Name);
            }

            var delay = isFastMode ? _fastInterval : _slowInterval;
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Stopping token was triggered - exit loop gracefully
                break;
            }
        }

        _logger.LogInformation("OutboxHostedService<{DbContextType}> stopped", typeof(TDbContext).Name);
    }
}
//TODO : Best Practice For Query Messages When It Have Too Much Messages (100,000) Waiting for review***
//TODO : Worker Is Working Single Thread ? Waiting for review***
//TODO : Event Type Invalid For Integration Events !!! Waiting for review***