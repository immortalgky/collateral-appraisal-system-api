using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Shared.Messaging.OutboxPatterns.Services;

namespace Shared.Messaging.OutboxPatterns.Jobs;

public class InboxCleanupJob<TDbContext> : IJob
    where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly InboxCleanupOptions _options;
    private readonly ILogger<InboxCleanupJob<TDbContext>> _logger;
    public InboxCleanupJob(
        IServiceProvider serviceProvider,
        IOptions<InboxCleanupOptions> options,
        ILogger<InboxCleanupJob<TDbContext>> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        if (!_options.Enabled) { _logger.LogDebug("🚫 Inbox cleanup job is disabled"); return; }

        var job = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var scope = _serviceProvider.CreateScope();

            var inboxService = scope.ServiceProvider.GetKeyedService<IInboxService>(typeof(TDbContext).Name);

            if (inboxService is null) { _logger.LogWarning("⚠️ Inbox service not found for {DbContext}", typeof(TDbContext).Name); return; }

            await inboxService.ClearTimeOutMessage();

            job.Stop();

            _logger.LogInformation("✅ Inbox cleanup completed in {Duration}ms", job.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            job.Stop();

            _logger.LogError(ex, "❌ Error during inbox cleanup after {Duration}ms", job.ElapsedMilliseconds);

            throw;

        }
    }

    
}