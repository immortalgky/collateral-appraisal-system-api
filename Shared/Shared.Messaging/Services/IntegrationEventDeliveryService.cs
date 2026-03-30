using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Data.Outbox;

namespace Shared.Messaging.Services;

public class IntegrationEventDeliveryService<TDbContext>(
    IServiceScopeFactory scopeFactory,
    ILogger<IntegrationEventDeliveryService<TDbContext>> logger)
    : BackgroundService where TDbContext : DbContext
{
    private const int BatchSize = 50;
    private const int MaxRetries = 5;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);

    private const string AllowedNamespace = "Shared.Messaging.Events";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _lockId = typeof(TDbContext).Name;
    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[OUTBOX] Delivery service started for {DbContext} (instance: {InstanceId})",
            _lockId, _instanceId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

                if (!await TryAcquireLeaseAsync(dbContext, stoppingToken))
                {
                    await Task.Delay(PollInterval, stoppingToken);
                    continue;
                }

                var bus = scope.ServiceProvider.GetRequiredService<IBus>();
                var processedCount = await ProcessBatchAsync(dbContext, bus, stoppingToken);

                if (processedCount == 0)
                    await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[OUTBOX] Error in delivery service for {DbContext}", _lockId);
                await Task.Delay(PollInterval, stoppingToken);
            }
        }

        // Graceful shutdown: release the lease
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
            await ReleaseLeaseAsync(dbContext, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[OUTBOX] Failed to release lease on shutdown for {DbContext}", _lockId);
        }

        logger.LogInformation("[OUTBOX] Delivery service stopped for {DbContext}", _lockId);
    }

    private async Task<bool> TryAcquireLeaseAsync(TDbContext dbContext, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var leasedUntil = now.Add(LeaseDuration);
        var schema = dbContext.Model.GetDefaultSchema() ?? "dbo";

        // Try to renew existing lease or claim an expired one
        var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE [" + schema + "].[OutboxDeliveryLock] " +
            "SET InstanceId = {0}, LeasedUntil = {1}, AcquiredAt = {2} " +
            "WHERE Id = {3} AND (InstanceId = {0} OR LeasedUntil < {2})",
            new object[] { _instanceId, leasedUntil, now, _lockId }, ct);

        if (rowsAffected > 0)
            return true;

        // Try to insert if no row exists (first time)
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO [" + schema + "].[OutboxDeliveryLock] (Id, InstanceId, LeasedUntil, AcquiredAt) " +
                "SELECT {0}, {1}, {2}, {3} " +
                "WHERE NOT EXISTS (SELECT 1 FROM [" + schema + "].[OutboxDeliveryLock] WHERE Id = {0})",
                new object[] { _lockId, _instanceId, leasedUntil, now }, ct);

            return true;
        }
        catch (DbUpdateException)
        {
            // Another instance inserted first — PK violation, not an error
            return false;
        }
    }

    private async Task ReleaseLeaseAsync(TDbContext dbContext, CancellationToken ct)
    {
        var schema = dbContext.Model.GetDefaultSchema() ?? "dbo";

        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE [" + schema + "].[OutboxDeliveryLock] " +
            "SET LeasedUntil = {0} " +
            "WHERE Id = {1} AND InstanceId = {2}",
            new object[] { DateTime.UtcNow, _lockId, _instanceId }, ct);
    }

    private async Task<int> ProcessBatchAsync(TDbContext dbContext, IBus bus, CancellationToken stoppingToken)
    {
        var messages = await dbContext.Set<IntegrationEventOutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(stoppingToken);

        if (messages.Count == 0)
            return 0;

        // Mark all as Processing first — prevents another instance from picking them up
        // if our lease expires during a long batch
        foreach (var msg in messages)
            msg.MarkAsProcessing();
        await dbContext.SaveChangesAsync(stoppingToken);

        // Group by CorrelationId for ordered delivery within correlation
        var groups = messages.GroupBy(m => m.CorrelationId ?? m.Id.ToString());

        var processedCount = 0;
        foreach (var group in groups)
        {
            foreach (var message in group.OrderBy(m => m.OccurredAt))
            {
                try
                {
                    var eventType = Type.GetType(message.EventType);

                    // Security: restrict to known integration event namespace
                    if (eventType == null || eventType.Namespace != AllowedNamespace)
                    {
                        logger.LogError("[OUTBOX] Disallowed or unresolvable type {EventType} for message {MessageId}",
                            message.EventType, message.Id);
                        message.IncrementRetryCount($"Disallowed type: {message.EventType}", MaxRetries);
                        continue;
                    }

                    var eventObject = JsonSerializer.Deserialize(message.Payload, eventType, SerializerOptions);
                    if (eventObject == null)
                    {
                        logger.LogError("[OUTBOX] Failed to deserialize message {MessageId}", message.Id);
                        message.IncrementRetryCount("Deserialization returned null", MaxRetries);
                        continue;
                    }

                    await bus.Publish(eventObject, eventType, stoppingToken);
                    message.MarkAsProcessed();
                    processedCount++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[OUTBOX] Failed to deliver message {MessageId} (retry {RetryCount})",
                        message.Id, message.RetryCount + 1);

                    message.IncrementRetryCount(ex.Message, MaxRetries);

                    // Stop processing this correlation group, continue others
                    break;
                }
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);

        if (processedCount > 0)
        {
            logger.LogInformation("[OUTBOX] Delivered {Count} messages from {DbContext}",
                processedCount, _lockId);
        }

        return processedCount;
    }
}
