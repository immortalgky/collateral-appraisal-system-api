using Common.Infrastructure;
using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Common.Application.EventHandlers;

public class CompanyAppraisalCompletedDashboardEventHandler(
    ISqlConnectionFactory connectionFactory,
    ILogger<CompanyAppraisalCompletedDashboardEventHandler> logger,
    InboxGuard<CommonDbContext> inboxGuard,
    IDateTimeProvider dateTimeProvider) : IConsumer<ExternalAppraisalReturnedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ExternalAppraisalReturnedIntegrationEvent> context)
    {
        var message = context.Message;

        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var connection = connectionFactory.GetOpenConnection();

        // Every cycle close is a real submission. CompletedCount increments only on CycleNumber == 1
        // to preserve the original first-proceed semantics.
        await connection.ExecuteAsync("""
            MERGE common.CompanyAppraisalSummaries AS target
            USING (SELECT @CompanyId AS CompanyId, @Date AS Date) AS source
            ON target.CompanyId = source.CompanyId AND target.Date = source.Date
            WHEN MATCHED THEN
                UPDATE SET
                    SubmissionCount    = SubmissionCount + 1,
                    TotalBusinessMinutes = TotalBusinessMinutes + @BusinessMinutes,
                    CompletedCount     = CompletedCount + CASE WHEN @CycleNumber = 1 THEN 1 ELSE 0 END,
                    LastUpdatedAt      = @Now
            WHEN NOT MATCHED THEN
                INSERT (CompanyId, Date, CompanyName, AssignedCount, CompletedCount, SubmissionCount, TotalBusinessMinutes, LastUpdatedAt)
                VALUES (@CompanyId, @Date, N'(pending)', 0,
                        CASE WHEN @CycleNumber = 1 THEN 1 ELSE 0 END,
                        1, @BusinessMinutes, @Now);
            """,
            new
            {
                CompanyId = message.CompanyId,
                Date = message.ClosedAt.Date,
                BusinessMinutes = (long)message.BusinessMinutes,
                CycleNumber = message.CycleNumber,
                Now = dateTimeProvider.ApplicationNow
            });

        logger.LogInformation(
            "Dashboard: CompanyAppraisalSummaries updated for CompanyId {CompanyId} Cycle={Cycle} BusinessMinutes={Minutes}",
            message.CompanyId, message.CycleNumber, message.BusinessMinutes);

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
