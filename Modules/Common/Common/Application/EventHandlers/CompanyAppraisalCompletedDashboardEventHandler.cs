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
    IDateTimeProvider dateTimeProvider) : IConsumer<TaskCompletedIntegrationEvent>
{
    private const string ExtVerificationTaskName = "ExtAppraisalVerification";
    private const string ProceedAction = "P";

    public async Task Consume(ConsumeContext<TaskCompletedIntegrationEvent> context)
    {
        var message = context.Message;

        if (message.TaskName != ExtVerificationTaskName || message.ActionTaken != ProceedAction)
            return;

        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var connection = connectionFactory.GetOpenConnection();

        // Route-back guard: only the FIRST Proceed counts. If ExtAppraisalVerification
        // has already been completed with "P" for this correlation, the bank routed it
        // back and the verifier re-proceeded — don't double-count.
        var proceedCount = await connection.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)
            FROM workflow.CompletedTasks
            WHERE CorrelationId = @CorrelationId
              AND TaskName = @TaskName
              AND ActionTaken = @ActionTaken
            """,
            new
            {
                message.CorrelationId,
                TaskName = ExtVerificationTaskName,
                ActionTaken = ProceedAction
            });

        if (proceedCount > 1)
        {
            logger.LogInformation(
                "Dashboard: ExtAppraisalVerification re-proceed detected for CorrelationId {CorrelationId} (count={Count}); skipping increment",
                message.CorrelationId, proceedCount);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
            return;
        }

        var companyIdRaw = await connection.QueryFirstOrDefaultAsync<string?>("""
            SELECT aa.AssigneeCompanyId
            FROM appraisal.Appraisals a
            INNER JOIN appraisal.AppraisalAssignments aa ON a.Id = aa.AppraisalId
            WHERE a.RequestId = @RequestId AND aa.AssigneeCompanyId IS NOT NULL
            """,
            new { RequestId = message.CorrelationId });

        if (string.IsNullOrEmpty(companyIdRaw) || !Guid.TryParse(companyIdRaw, out var companyId))
        {
            logger.LogWarning(
                "Dashboard: ExtAppraisalVerification proceeded for CorrelationId {CorrelationId} but no external company assignment found",
                message.CorrelationId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
            return;
        }

        // MERGE (not plain UPDATE): if CompanyAssigned event hasn't been processed
        // yet for some reason, we still record the completion. CompanyName gets
        // patched by the subsequent CompanyAssigned MERGE.
        await connection.ExecuteAsync("""
            MERGE common.CompanyAppraisalSummaries AS target
            USING (SELECT @CompanyId AS CompanyId) AS source
            ON target.CompanyId = source.CompanyId
            WHEN MATCHED THEN
                UPDATE SET CompletedCount = CompletedCount + 1, LastUpdatedAt = @Now
            WHEN NOT MATCHED THEN
                INSERT (CompanyId, CompanyName, AssignedCount, CompletedCount, LastUpdatedAt)
                VALUES (@CompanyId, N'(pending)', 0, 1, @Now);
            """,
            new { CompanyId = companyId, Now = dateTimeProvider.ApplicationNow });

        logger.LogInformation(
            "Dashboard: CompanyAppraisalSummaries.CompletedCount incremented for CompanyId {CompanyId} (CorrelationId {CorrelationId})",
            companyId, message.CorrelationId);

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
