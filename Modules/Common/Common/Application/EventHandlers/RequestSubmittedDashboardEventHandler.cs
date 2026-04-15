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
        var now = DateTime.UtcNow;

        // Decrement DRAFT, increment SUBMITTED — both in a transaction to keep counts consistent
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync("""
                MERGE common.RequestStatusSummaries WITH (HOLDLOCK) AS target
                USING (SELECT 'Draft' AS Status) AS source
                ON target.Status = source.Status
                WHEN MATCHED THEN
                    UPDATE SET Count = CASE WHEN Count > 0 THEN Count - 1 ELSE 0 END, LastUpdatedAt = @Now
                WHEN NOT MATCHED THEN
                    INSERT (Status, Count, LastUpdatedAt)
                    VALUES ('Draft', 0, @Now);
                """,
                new { Now = now }, transaction: transaction);

            await connection.ExecuteAsync("""
                MERGE common.RequestStatusSummaries WITH (HOLDLOCK) AS target
                USING (SELECT 'Submitted' AS Status) AS source
                ON target.Status = source.Status
                WHEN MATCHED THEN
                    UPDATE SET Count = Count + 1, LastUpdatedAt = @Now
                WHEN NOT MATCHED THEN
                    INSERT (Status, Count, LastUpdatedAt)
                    VALUES ('Submitted', 1, @Now);
                """,
                new { Now = now }, transaction: transaction);

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
