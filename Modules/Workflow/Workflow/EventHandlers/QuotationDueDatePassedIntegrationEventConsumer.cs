using MassTransit;
using MediatR;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.Tasks.Features.ExpireOverdueFanOutTasks;

namespace Workflow.EventHandlers;

/// <summary>
/// Expires overdue fan-out PendingTasks and resumes the quotation child workflow
/// when a quotation's DueDate passes.
///
/// Flow:
///   QuotationAutoCloseService → QuotationDueDatePassedIntegrationEvent
///     → this consumer → ExpireOverdueFanOutTasksCommand (archives tasks, resumes workflow)
///     → QuotationCompaniesAutoExpiredIntegrationEvent (signals Appraisal to auto-decline)
///
/// Idempotent: ExpireOverdueFanOutTasksCommand is a no-op when no overdue tasks remain.
/// </summary>
public class QuotationDueDatePassedIntegrationEventConsumer(
    ISender mediator,
    IPublishEndpoint publishEndpoint,
    InboxGuard<WorkflowDbContext> inboxGuard,
    ILogger<QuotationDueDatePassedIntegrationEventConsumer> logger)
    : IConsumer<QuotationDueDatePassedIntegrationEvent>
{
    private const string DefaultActivityId = "ext-collect-submissions";

    public async Task Consume(ConsumeContext<QuotationDueDatePassedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "QuotationDueDatePassedConsumer: processing QuotationRequestId={QuotationRequestId} WorkflowInstanceId={WorkflowInstanceId}",
            message.QuotationRequestId, message.QuotationWorkflowInstanceId);

        if (message.QuotationWorkflowInstanceId is null)
        {
            logger.LogWarning(
                "QuotationDueDatePassedConsumer: QuotationRequestId={QuotationRequestId} has no QuotationWorkflowInstanceId — skipping fan-out expiry",
                message.QuotationRequestId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        var result = await mediator.Send(
            new ExpireOverdueFanOutTasksCommand(
                message.QuotationWorkflowInstanceId.Value,
                DefaultActivityId),
            ct);

        logger.LogInformation(
            "QuotationDueDatePassedConsumer: archived {ArchivedCount} overdue task(s) for workflow {WorkflowInstanceId}",
            result.ArchivedCount, message.QuotationWorkflowInstanceId.Value);

        // Notify Appraisal module to auto-decline companies that never responded
        if (result.ExpiredCompanyIds.Count > 0)
        {
            await publishEndpoint.Publish(new QuotationCompaniesAutoExpiredIntegrationEvent
            {
                QuotationRequestId = message.QuotationRequestId,
                ExpiredCompanyIds = result.ExpiredCompanyIds
            }, ct);

            logger.LogInformation(
                "QuotationDueDatePassedConsumer: published auto-expire for {Count} company/ies on QuotationRequest {QuotationRequestId}",
                result.ExpiredCompanyIds.Count, message.QuotationRequestId);
        }

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
    }
}
