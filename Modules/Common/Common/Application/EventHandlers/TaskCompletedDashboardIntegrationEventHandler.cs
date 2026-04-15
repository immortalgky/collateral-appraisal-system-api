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
        {
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
            return;
        }

        // Decrement the bucket the task previously occupied on the original assignee.
        var originalAssignedTo = string.IsNullOrEmpty(message.OriginalAssignedTo)
            ? completedBy
            : message.OriginalAssignedTo;

        var connection = connectionFactory.GetOpenConnection();
        await connection.ExecuteAsync("""
            UPDATE common.TeamWorkloadSummaries
            SET
                NotStarted = CASE WHEN @WasStarted = 0 AND NotStarted > 0 THEN NotStarted - 1 ELSE NotStarted END,
                InProgress = CASE WHEN @WasStarted = 1 AND InProgress > 0 THEN InProgress - 1 ELSE InProgress END,
                LastUpdatedAt = @Now
            WHERE Username = @Username
            """,
            new
            {
                Username = originalAssignedTo,
                WasStarted = message.WasStarted ? 1 : 0,
                Now = DateTime.UtcNow
            });

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
