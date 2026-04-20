using Common.Infrastructure;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Common.Application.EventHandlers;

public class AppraisalCompletedDashboardEventHandler(
    ISqlConnectionFactory connectionFactory,
    ILogger<AppraisalCompletedDashboardEventHandler> logger,
    InboxGuard<CommonDbContext> inboxGuard,
    IDateTimeProvider dateTimeProvider) : IConsumer<AppraisalCompletedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalCompletedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Dashboard: AppraisalCompleted for RequestId {RequestId}",
            message.RequestId);

        var connection = connectionFactory.GetOpenConnection();
        var today = message.CompletedAt.Date;

        await connection.ExecuteAsync("""
            MERGE common.DailyAppraisalCounts AS target
            USING (SELECT @Date AS Date) AS source
            ON target.Date = source.Date
            WHEN MATCHED THEN
                UPDATE SET CompletedCount = CompletedCount + 1, LastUpdatedAt = @Now
            WHEN NOT MATCHED THEN
                INSERT (Date, CreatedCount, CompletedCount, LastUpdatedAt)
                VALUES (@Date, 0, 1, @Now);
            """,
            new { Date = today, Now = dateTimeProvider.ApplicationNow });

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
