using Common.Infrastructure;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Common.Application.EventHandlers;

public class AppraisalCreatedDashboardEventHandler(
    ISqlConnectionFactory connectionFactory,
    ILogger<AppraisalCreatedDashboardEventHandler> logger,
    InboxGuard<CommonDbContext> inboxGuard,
    IDateTimeProvider dateTimeProvider) : IConsumer<AppraisalCreatedIntegrationEvent>
{
    public Task Consume(ConsumeContext<AppraisalCreatedIntegrationEvent> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, _ => HandleAsync(context), context.CancellationToken);

    private async Task HandleAsync(ConsumeContext<AppraisalCreatedIntegrationEvent> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Dashboard: AppraisalCreated for AppraisalId {AppraisalId}",
            message.AppraisalId);

        var connection = connectionFactory.GetOpenConnection();
        var today = message.CreatedAt.Date;

        await connection.ExecuteAsync("""
            MERGE common.DailyAppraisalCounts AS target
            USING (SELECT @Date AS Date) AS source
            ON target.Date = source.Date
            WHEN MATCHED THEN
                UPDATE SET CreatedCount = CreatedCount + 1, LastUpdatedAt = @Now
            WHEN NOT MATCHED THEN
                INSERT (Date, CreatedCount, CompletedCount, LastUpdatedAt)
                VALUES (@Date, 1, 0, @Now);
            """,
            new { Date = today, Now = dateTimeProvider.ApplicationNow });
    }
}
