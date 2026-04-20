using Common.Infrastructure;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Common.Application.EventHandlers;

public class CompanyAssignedDashboardEventHandler(
    ISqlConnectionFactory connectionFactory,
    ILogger<CompanyAssignedDashboardEventHandler> logger,
    InboxGuard<CommonDbContext> inboxGuard,
    IDateTimeProvider dateTimeProvider) : IConsumer<CompanyAssignedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CompanyAssignedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;

        logger.LogInformation(
            "Dashboard: CompanyAssigned for AppraisalId {AppraisalId}, CompanyId {CompanyId}",
            message.AppraisalId, message.CompanyId);

        var connection = connectionFactory.GetOpenConnection();

        await connection.ExecuteAsync("""
            MERGE common.CompanyAppraisalSummaries AS target
            USING (SELECT @CompanyId AS CompanyId, @Date AS Date) AS source
            ON target.CompanyId = source.CompanyId AND target.Date = source.Date
            WHEN MATCHED THEN
                UPDATE SET AssignedCount = AssignedCount + 1,
                           CompanyName = @CompanyName,
                           LastUpdatedAt = @Now
            WHEN NOT MATCHED THEN
                INSERT (CompanyId, Date, CompanyName, AssignedCount, CompletedCount, LastUpdatedAt)
                VALUES (@CompanyId, @Date, @CompanyName, 1, 0, @Now);
            """,
            new
            {
                message.CompanyId,
                message.CompanyName,
                Date = message.OccurredOn.Date,
                Now = dateTimeProvider.ApplicationNow
            });

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
