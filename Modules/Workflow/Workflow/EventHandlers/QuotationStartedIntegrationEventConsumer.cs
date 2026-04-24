using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.Workflow.Infrastructure.Seed;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;

namespace Workflow.EventHandlers;

/// <summary>
/// Spawns the quotation child workflow when a QuotationRequest transitions to Sent.
/// CorrelationId = QuotationRequestId so task-list and resume calls scope correctly.
///
/// Idempotent: if a workflow instance already exists for the same CorrelationId it is
/// a retry/duplicate — the consumer logs and exits without spawning a second instance.
/// </summary>
public class QuotationStartedIntegrationEventConsumer(
    IWorkflowDefinitionRepository definitionRepository,
    IWorkflowInstanceRepository instanceRepository,
    IWorkflowService workflowService,
    InboxGuard<WorkflowDbContext> inboxGuard,
    ILogger<QuotationStartedIntegrationEventConsumer> logger) : IConsumer<QuotationStartedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<QuotationStartedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "QuotationStartedIntegrationEventConsumer: QuotationRequestId={QuotationRequestId}",
            message.QuotationRequestId);

        // Idempotency: do not spawn a second instance for the same quotation.
        // Use the definition-scoped overload to avoid false-positives when another
        // workflow type happens to share the same CorrelationId value.
        var existing = await instanceRepository.GetByCorrelationIdAsync(
            message.QuotationRequestId.ToString(),
            QuotationWorkflowDefinitionSeeder.WorkflowName,
            ct);

        if (existing is not null)
        {
            logger.LogInformation(
                "Quotation workflow already exists {InstanceId} for QuotationRequestId={QuotationRequestId} — skipping spawn",
                existing.Id, message.QuotationRequestId);
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
            return;
        }

        var definition = await definitionRepository.GetLatestVersion(
                             QuotationWorkflowDefinitionSeeder.WorkflowName, ct)
                         ?? throw new InvalidOperationException(
                             $"Workflow definition '{QuotationWorkflowDefinitionSeeder.WorkflowName}' not found. " +
                             "Did the seeder run?");

        // Build initial workflow variables — mirrors the quotation-workflow.json variable schema.
        // Both startedByUserId and rmUserId hold usernames (not Guids) so that workflow
        // assignee selectors (StartedByAssigneeSelector, VariableAssigneeSelector) resolve correctly.
        var initialVariables = new Dictionary<string, object>
        {
            ["quotationRequestId"] = message.QuotationRequestId,
            ["appraisalIds"] = message.AppraisalIds.Length > 0
                ? (object)message.AppraisalIds
                : new[] { message.AppraisalId },
            ["invitedCompanyIds"] = message.InvitedCompanyIds,
            ["dueDate"] = message.DueDate,
            ["startedByUserId"] = message.StartedByUsername ?? string.Empty,
            ["rmUserId"] = message.RmUsername ?? string.Empty,
            ["tentativeWinnerCompanyQuotationId"] = (object?)null!,
            ["tentativeWinnerCompanyId"] = (object?)null!,
            ["rmRequestsNegotiation"] = false,
            ["rmNegotiationNote"] = (object?)null!,
            ["negotiationRound"] = (object?)null!
        };

        var startedBy = message.StartedByUsername ?? "system";

        var instance = await workflowService.StartWorkflowAsync(
            definition.Id,
            $"Quotation-{message.QuotationRequestId}",
            startedBy,
            initialVariables,
            correlationId: message.QuotationRequestId.ToString(),
            cancellationToken: ct);

        logger.LogInformation(
            "Spawned quotation workflow instance {InstanceId} for QuotationRequestId={QuotationRequestId}",
            instance.Id, message.QuotationRequestId);

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
    }
}
