using Common.Infrastructure;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Common.Application.EventHandlers;

public class TaskStartedDashboardIntegrationEventHandler(
    ISqlConnectionFactory connectionFactory,
    ILogger<TaskStartedDashboardIntegrationEventHandler> logger,
    InboxGuard<CommonDbContext> inboxGuard) : IConsumer<TaskStartedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<TaskStartedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Dashboard: TaskStarted for CorrelationId {CorrelationId}, AssignedTo {AssignedTo}",
            message.CorrelationId, message.AssignedTo);

        var connection = connectionFactory.GetOpenConnection();
        var now = DateTime.UtcNow;

        // For pool tasks, PreviousAssignedTo is the pool group name — decrement there.
        // For non-pool tasks, PreviousAssignedTo is null and the same row is updated twice.
        var notStartedUsername = message.PreviousAssignedTo ?? message.AssignedTo;

        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync("""
                UPDATE common.TeamWorkloadSummaries
                SET NotStarted = CASE WHEN NotStarted > 0 THEN NotStarted - 1 ELSE 0 END,
                    LastUpdatedAt = @Now
                WHERE Username = @Username
                """,
                new { Username = notStartedUsername, Now = now },
                transaction: transaction);

            await connection.ExecuteAsync("""
                MERGE common.TeamWorkloadSummaries WITH (HOLDLOCK) AS target
                USING (SELECT @Username AS Username) AS source
                ON target.Username = source.Username
                WHEN MATCHED THEN
                    UPDATE SET InProgress = InProgress + 1, LastUpdatedAt = @Now
                WHEN NOT MATCHED THEN
                    INSERT (Username, TeamId, NotStarted, InProgress, Completed, LastUpdatedAt)
                    VALUES (@Username, NULL, 0, 1, 0, @Now);
                """,
                new { Username = message.AssignedTo, Now = now },
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
