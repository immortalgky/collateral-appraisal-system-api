using MassTransit;
using MediatR;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.Tasks.Features.ExpireOverdueFanOutTasks;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;

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
    IWorkflowInstanceRepository instanceRepository,
    InboxGuard<WorkflowDbContext> inboxGuard,
    ILogger<QuotationDueDatePassedIntegrationEventConsumer> logger)
    : IConsumer<QuotationDueDatePassedIntegrationEvent>
{
    private const string DefaultActivityId = "ext-collect-submissions";
    private const string QuotationWorkflowDefinitionName = "Quotation Workflow";

    public async Task Consume(ConsumeContext<QuotationDueDatePassedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "QuotationDueDatePassedConsumer: processing QuotationRequestId={QuotationRequestId}",
            message.QuotationRequestId);

        // Resolve the child workflow by CorrelationId (set at spawn in
        // QuotationStartedIntegrationEventConsumer to QuotationRequestId.ToString()).
        // QuotationRequest.QuotationWorkflowInstanceId is never written, so we cannot
        // trust the value on the incoming message. Scope by workflow definition name
        // because (CorrelationId, WorkflowDefinitionId) is the DB uniqueness key —
        // other workflow types could share the same CorrelationId value.
        var instance = await instanceRepository.GetByCorrelationIdAsync(
            message.QuotationRequestId.ToString(),
            QuotationWorkflowDefinitionName,
            ct);

        if (instance is null)
        {
            logger.LogWarning(
                "QuotationDueDatePassedConsumer: no workflow instance found by CorrelationId for QuotationRequestId={QuotationRequestId} — skipping fan-out expiry",
                message.QuotationRequestId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        if (instance.Status is WorkflowStatus.Completed
                               or WorkflowStatus.Cancelled
                               or WorkflowStatus.Failed)
        {
            logger.LogInformation(
                "QuotationDueDatePassedConsumer: workflow {InstanceId} already terminal ({Status}) — skipping",
                instance.Id, instance.Status);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        var result = await mediator.Send(
            new ExpireOverdueFanOutTasksCommand(
                instance.Id,
                DefaultActivityId),
            ct);

        logger.LogInformation(
            "QuotationDueDatePassedConsumer: archived {ArchivedCount} overdue task(s) for workflow {WorkflowInstanceId}",
            result.ArchivedCount, instance.Id);

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
