using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;

namespace Workflow.EventHandlers;

/// <summary>
/// Drives the quotation child workflow forward by calling ResumeWorkflowAsync (or CancelWorkflowAsync)
/// when a quotation command handler publishes a QuotationWorkflowResumeIntegrationEvent.
///
/// Lookup: resolves the child workflow instance via CorrelationId = QuotationRequestId.
/// Idempotent: if the workflow instance is already terminal or cannot be found, the message is ACKed safely.
/// Cancel sentinel: ActivityId == "cancel" triggers CancelWorkflowAsync instead of ResumeWorkflowAsync.
/// </summary>
public class QuotationWorkflowResumeIntegrationEventConsumer(
    IWorkflowInstanceRepository instanceRepository,
    IWorkflowService workflowService,
    InboxGuard<WorkflowDbContext> inboxGuard,
    ILogger<QuotationWorkflowResumeIntegrationEventConsumer> logger)
    : IConsumer<QuotationWorkflowResumeIntegrationEvent>
{
    private const string CancelSentinel = "cancel";

    public async Task Consume(ConsumeContext<QuotationWorkflowResumeIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "QuotationWorkflowResumeConsumer: QuotationRequestId={QuotationRequestId} ActivityId={ActivityId} Decision={Decision}",
            message.QuotationRequestId, message.ActivityId, message.DecisionTaken);

        var instance = await instanceRepository.GetByCorrelationId(
            message.QuotationRequestId.ToString(), ct);

        if (instance is null)
        {
            logger.LogWarning(
                "QuotationWorkflowResumeConsumer: no workflow instance found for QuotationRequestId={QuotationRequestId} — skipping",
                message.QuotationRequestId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        // If workflow is already terminal, nothing to drive
        if (instance.Status is WorkflowStatus.Completed or WorkflowStatus.Cancelled or WorkflowStatus.Failed)
        {
            logger.LogInformation(
                "QuotationWorkflowResumeConsumer: workflow {InstanceId} is already {Status} — skipping resume",
                instance.Id, instance.Status);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        if (message.ActivityId == CancelSentinel)
        {
            await workflowService.CancelWorkflowAsync(
                instance.Id,
                message.CompletedBy,
                $"Quotation cancelled (QuotationRequestId={message.QuotationRequestId})",
                ct);

            logger.LogInformation(
                "QuotationWorkflowResumeConsumer: cancelled workflow {InstanceId}", instance.Id);
        }
        else
        {
            var resumeInput = BuildResumeInput(message);

            await workflowService.ResumeWorkflowAsync(
                instance.Id,
                message.ActivityId,
                message.CompletedBy,
                resumeInput,
                cancellationToken: ct);

            logger.LogInformation(
                "QuotationWorkflowResumeConsumer: resumed workflow {InstanceId} at activity {ActivityId} with decision {Decision}",
                instance.Id, message.ActivityId, message.DecisionTaken);
        }

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
    }

    private static Dictionary<string, object> BuildResumeInput(QuotationWorkflowResumeIntegrationEvent message)
    {
        var input = new Dictionary<string, object>
        {
            ["decisionTaken"] = message.DecisionTaken,
            ["completedBy"] = message.CompletedBy
        };

        if (message.CompanyId.HasValue)
            input["companyId"] = message.CompanyId.Value;

        if (message.TentativeWinnerCompanyQuotationId.HasValue)
            input["tentativeWinnerCompanyQuotationId"] = message.TentativeWinnerCompanyQuotationId.Value;

        if (message.TentativeWinnerCompanyId.HasValue)
            input["tentativeWinnerCompanyId"] = message.TentativeWinnerCompanyId.Value;

        // Always write negotiation vars so workflow variables are cleared when the flag is false
        input["rmRequestsNegotiation"] = message.RmRequestsNegotiation;
        input["rmNegotiationNote"] = message.RmNegotiationNote ?? (object)string.Empty;

        return input;
    }
}
