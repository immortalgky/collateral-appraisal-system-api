using Common.Infrastructure;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Common.Application.EventHandlers;

public class TaskCompletedDashboardIntegrationEventHandler(
    ISqlConnectionFactory connectionFactory,
    ILogger<TaskCompletedDashboardIntegrationEventHandler> logger,
    InboxGuard<CommonDbContext> inboxGuard) : IConsumer<TaskCompletedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<TaskCompletedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Dashboard: TaskCompleted for CorrelationId {CorrelationId}",
            message.CorrelationId);

        var completedBy = message.CompletedBy;
        if (string.IsNullOrEmpty(completedBy))
            return;

        var connection = connectionFactory.GetOpenConnection();
        var today = DateTime.UtcNow.Date;

        // Update DailyTaskSummary — decrement InProgress, increment Completed
        await connection.ExecuteAsync("""
            MERGE common.DailyTaskSummaries AS target
            USING (SELECT @Date AS Date, @Username AS Username) AS source
            ON target.Date = source.Date AND target.Username = source.Username
            WHEN MATCHED THEN
                UPDATE SET InProgress = CASE WHEN InProgress > 0 THEN InProgress - 1 ELSE 0 END,
                           Completed = Completed + 1,
                           LastUpdatedAt = @Now
            WHEN NOT MATCHED THEN
                INSERT (Date, Username, NotStarted, InProgress, Overdue, Completed, LastUpdatedAt)
                VALUES (@Date, @Username, 0, 0, 0, 1, @Now);
            """,
            new { Date = today, Username = completedBy, Now = DateTime.UtcNow });

        // Update TeamWorkloadSummary
        await connection.ExecuteAsync("""
            UPDATE common.TeamWorkloadSummaries
            SET InProgress = CASE WHEN InProgress > 0 THEN InProgress - 1 ELSE 0 END,
                Completed = Completed + 1,
                LastUpdatedAt = @Now
            WHERE Username = @Username
            """,
            new { Username = completedBy, Now = DateTime.UtcNow });

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
