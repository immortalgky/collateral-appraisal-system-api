using Common.Infrastructure;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Common.Application.EventHandlers;

public class RequestSubmittedDashboardEventHandler(
    ISqlConnectionFactory connectionFactory,
    ILogger<RequestSubmittedDashboardEventHandler> logger,
    InboxGuard<CommonDbContext> inboxGuard) : IConsumer<RequestSubmittedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<RequestSubmittedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Dashboard: RequestSubmitted for RequestId {RequestId}",
            message.RequestId);

        var connection = connectionFactory.GetOpenConnection();

        // Decrement DRAFT, increment SUBMITTED
        await connection.ExecuteAsync("""
            MERGE common.RequestStatusSummaries AS target
            USING (SELECT 'DRAFT' AS Status) AS source
            ON target.Status = source.Status
            WHEN MATCHED THEN
                UPDATE SET Count = CASE WHEN Count > 0 THEN Count - 1 ELSE 0 END, LastUpdatedAt = @Now
            WHEN NOT MATCHED THEN
                INSERT (Status, Count, LastUpdatedAt)
                VALUES ('DRAFT', 0, @Now);

            MERGE common.RequestStatusSummaries AS target
            USING (SELECT 'SUBMITTED' AS Status) AS source
            ON target.Status = source.Status
            WHEN MATCHED THEN
                UPDATE SET Count = Count + 1, LastUpdatedAt = @Now
            WHEN NOT MATCHED THEN
                INSERT (Status, Count, LastUpdatedAt)
                VALUES ('SUBMITTED', 1, @Now);
            """,
            new { Now = DateTime.UtcNow });

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
