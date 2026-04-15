using Common.Infrastructure;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Common.Application.EventHandlers;

public class TaskAssignedDashboardIntegrationEventHandler(
    ISqlConnectionFactory connectionFactory,
    ILogger<TaskAssignedDashboardIntegrationEventHandler> logger,
    InboxGuard<CommonDbContext> inboxGuard) : IConsumer<TaskAssignedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<TaskAssignedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Dashboard: TaskAssigned for CorrelationId {CorrelationId}, AssignedTo {AssignedTo}",
            message.CorrelationId, message.AssignedTo);

        var connection = connectionFactory.GetOpenConnection();
        var now = DateTime.UtcNow;

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
            new { Username = message.AssignedTo, Now = now });

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
