using Common.Infrastructure;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Common.Application.EventHandlers;

public class AppraisalCreatedDashboardStatusHandler(
    ISqlConnectionFactory connectionFactory,
    ILogger<AppraisalCreatedDashboardStatusHandler> logger,
    InboxGuard<CommonDbContext> inboxGuard,
    IDateTimeProvider dateTimeProvider) : IConsumer<AppraisalCreatedIntegrationEvent>
{
    public Task Consume(ConsumeContext<AppraisalCreatedIntegrationEvent> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, _ => HandleAsync(context), context.CancellationToken);

    private async Task HandleAsync(ConsumeContext<AppraisalCreatedIntegrationEvent> context)
    {
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
            new { Now = new DateTimeOffset(dateTimeProvider.ApplicationNow) });
    }
}
