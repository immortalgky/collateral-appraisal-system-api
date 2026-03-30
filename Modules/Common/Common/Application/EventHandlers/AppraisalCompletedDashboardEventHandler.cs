using Common.Infrastructure;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Common.Application.EventHandlers;

public class AppraisalCompletedDashboardEventHandler(
    ISqlConnectionFactory connectionFactory,
    ILogger<AppraisalCompletedDashboardEventHandler> logger,
    InboxGuard<CommonDbContext> inboxGuard) : IConsumer<AppraisalCompletedIntegrationEvent>
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

        // Update total daily counts
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
            new { Date = today, Now = DateTime.UtcNow });

        // Update company summary if assigned to external company
        var companyId = await connection.QueryFirstOrDefaultAsync<string?>("""
            SELECT aa.AssigneeCompanyId
            FROM appraisal.Appraisals a
            INNER JOIN appraisal.AppraisalAssignments aa ON a.Id = aa.AppraisalId
            WHERE a.RequestId = @RequestId AND aa.AssigneeCompanyId IS NOT NULL
            """,
            new { message.RequestId });

        if (!string.IsNullOrEmpty(companyId) && Guid.TryParse(companyId, out var parsedCompanyId))
        {
            await connection.ExecuteAsync("""
                UPDATE common.CompanyAppraisalSummaries
                SET CompletedCount = CompletedCount + 1, LastUpdatedAt = @Now
                WHERE CompanyId = @CompanyId
                """,
                new { CompanyId = parsedCompanyId, Now = DateTime.UtcNow });
        }

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
