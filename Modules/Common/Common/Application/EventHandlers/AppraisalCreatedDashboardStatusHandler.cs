using Common.Infrastructure;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Common.Application.EventHandlers;

public class AppraisalCreatedDashboardStatusHandler(
    ISqlConnectionFactory connectionFactory,
    ILogger<AppraisalCreatedDashboardStatusHandler> logger,
    InboxGuard<CommonDbContext> inboxGuard) : IConsumer<AppraisalCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalCreatedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Dashboard: AppraisalCreated — incrementing Pending status count for AppraisalId {AppraisalId}",
            message.AppraisalId);

        var connection = connectionFactory.GetOpenConnection();

        // TODO: derivation rules pending — for now, increment "Pending" count on creation.
        await connection.ExecuteAsync("""
            MERGE common.AppraisalStatusSummaries WITH (HOLDLOCK) AS target
            USING (SELECT 'Pending' AS Status) AS source
            ON target.Status = source.Status
            WHEN MATCHED THEN
                UPDATE SET Count = Count + 1, LastUpdatedAt = @Now
            WHEN NOT MATCHED THEN
                INSERT (Status, Count, LastUpdatedAt)
                VALUES ('Pending', 1, @Now);
            """,
            new { Now = DateTimeOffset.UtcNow });

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
