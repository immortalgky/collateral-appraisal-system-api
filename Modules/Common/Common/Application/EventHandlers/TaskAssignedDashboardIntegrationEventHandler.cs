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
        var today = DateTime.UtcNow.Date;

        // Upsert DailyTaskSummary — increment InProgress for the assignee
        await connection.ExecuteAsync("""
            MERGE common.DailyTaskSummaries AS target
            USING (SELECT @Date AS Date, @Username AS Username) AS source
            ON target.Date = source.Date AND target.Username = source.Username
            WHEN MATCHED THEN
                UPDATE SET InProgress = InProgress + 1, LastUpdatedAt = @Now
            WHEN NOT MATCHED THEN
                INSERT (Date, Username, NotStarted, InProgress, Overdue, Completed, LastUpdatedAt)
                VALUES (@Date, @Username, 0, 1, 0, 0, @Now);
            """,
            new { Date = today, Username = message.AssignedTo, Now = DateTime.UtcNow });

        // Upsert TeamWorkloadSummary
        await connection.ExecuteAsync("""
            MERGE common.TeamWorkloadSummaries AS target
            USING (SELECT @Username AS Username) AS source
            ON target.Username = source.Username
            WHEN MATCHED THEN
                UPDATE SET InProgress = InProgress + 1, LastUpdatedAt = @Now
            WHEN NOT MATCHED THEN
                INSERT (Username, TeamId, NotStarted, InProgress, Completed, LastUpdatedAt)
                VALUES (@Username, NULL, 0, 1, 0, @Now);
            """,
            new { Username = message.AssignedTo, Now = DateTime.UtcNow });

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
