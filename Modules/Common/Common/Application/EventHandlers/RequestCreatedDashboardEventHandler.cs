using Common.Infrastructure;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Common.Application.EventHandlers;

public class RequestCreatedDashboardEventHandler(
    ISqlConnectionFactory connectionFactory,
    ILogger<RequestCreatedDashboardEventHandler> logger,
    InboxGuard<CommonDbContext> inboxGuard) : IConsumer<RequestCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<RequestCreatedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Dashboard: RequestCreated for RequestId {RequestId}",
            message.RequestId);

        var connection = connectionFactory.GetOpenConnection();

        // Increment count for the initial status (NEW/DRAFT depending on flow)
        await connection.ExecuteAsync("""
            MERGE common.RequestStatusSummaries AS target
            USING (SELECT 'DRAFT' AS Status) AS source
            ON target.Status = source.Status
            WHEN MATCHED THEN
                UPDATE SET Count = Count + 1, LastUpdatedAt = @Now
            WHEN NOT MATCHED THEN
                INSERT (Status, Count, LastUpdatedAt)
                VALUES ('DRAFT', 1, @Now);
            """,
            new { Now = DateTime.UtcNow });

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
