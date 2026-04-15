using Common.Infrastructure;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Common.Application.EventHandlers;

public class TaskClaimedDashboardIntegrationEventHandler(
    ISqlConnectionFactory connectionFactory,
    ILogger<TaskClaimedDashboardIntegrationEventHandler> logger,
    InboxGuard<CommonDbContext> inboxGuard) : IConsumer<TaskClaimedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<TaskClaimedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Dashboard: TaskClaimed for CorrelationId {CorrelationId}, PoolGroup {PoolGroup}, ClaimedBy {ClaimedBy}",
            message.CorrelationId, message.PoolGroup, message.ClaimedBy);

        var connection = connectionFactory.GetOpenConnection();
        var now = DateTime.UtcNow;

        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync("""
                UPDATE common.TeamWorkloadSummaries
                SET NotStarted = CASE WHEN NotStarted > 0 THEN NotStarted - 1 ELSE 0 END, LastUpdatedAt = @Now
                WHERE Username = @Username
                """,
                new { Username = message.PoolGroup, Now = now },
                transaction: transaction);

            await connection.ExecuteAsync("""
                MERGE common.TeamWorkloadSummaries WITH (HOLDLOCK) AS target
                USING (SELECT @Username AS Username) AS source
                ON target.Username = source.Username
                WHEN MATCHED THEN
                    UPDATE SET NotStarted = NotStarted + 1, LastUpdatedAt = @Now
                WHEN NOT MATCHED THEN
                    INSERT (Username, TeamId, NotStarted, InProgress, Completed, LastUpdatedAt)
                    VALUES (@Username, NULL, 1, 0, 0, @Now);
                """,
                new { Username = message.ClaimedBy, Now = now },
                transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
