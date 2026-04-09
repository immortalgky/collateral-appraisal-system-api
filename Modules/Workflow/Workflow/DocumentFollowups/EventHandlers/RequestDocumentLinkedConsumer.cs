using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.DocumentFollowups.Domain;
using Workflow.Workflow.Services;

namespace Workflow.DocumentFollowups.EventHandlers;

/// <summary>
/// Listens for document uploads against a Request and auto-fulfills the first matching
/// open document followup line item. When all line items resolve, the followup workflow
/// is auto-resumed and ends.
/// </summary>
public class RequestDocumentLinkedConsumer(
    WorkflowDbContext dbContext,
    IWorkflowService workflowService,
    IPublisher publisher,
    InboxGuard<WorkflowDbContext> inboxGuard,
    ILogger<RequestDocumentLinkedConsumer> logger)
    : IConsumer<DocumentLinkedIntegrationEventV2>
{
    public async Task Consume(ConsumeContext<DocumentLinkedIntegrationEventV2> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var msg = context.Message;
        if (string.IsNullOrWhiteSpace(msg.DocumentType))
        {
            // Cannot match without document type — just ack.
            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
            return;
        }

        // Find candidate open followups for this request
        var openFollowups = await dbContext.DocumentFollowups
            .Where(f => f.RequestId == msg.RequestId && f.Status == DocumentFollowupStatus.Open)
            .ToListAsync(context.CancellationToken);

        foreach (var followup in openFollowups)
        {
            var matchedLineItemId = followup.FulfillFirstMatchingByType(msg.DocumentType, msg.DocumentId);
            if (matchedLineItemId is null) continue;

            await dbContext.SaveChangesAsync(context.CancellationToken);

            foreach (var ev in followup.ClearDomainEvents())
                await publisher.Publish(ev, context.CancellationToken);

            logger.LogInformation(
                "Auto-fulfilled line item {LineItemId} on followup {FollowupId} via document {DocumentId}",
                matchedLineItemId, followup.Id, msg.DocumentId);

            // If the followup is now resolved, advance the followup workflow.
            if (followup.Status == DocumentFollowupStatus.Resolved &&
                followup.FollowupWorkflowInstanceId.HasValue)
            {
                try
                {
                    await AdvanceFollowupWorkflowAsync(followup.FollowupWorkflowInstanceId.Value, context.CancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Failed to advance followup workflow {InstanceId} after auto-fulfill",
                        followup.FollowupWorkflowInstanceId);
                }
            }

            // First match wins per upload — stop scanning further followups.
            break;
        }

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }

    private async Task AdvanceFollowupWorkflowAsync(Guid followupWorkflowInstanceId, CancellationToken ct)
    {
        // Resume the ProvideAdditionalDocuments task; the system NotifyRaisingChecker task that
        // follows is auto-resumable too. We delegate to WorkflowService.
        var instance = await dbContext.WorkflowInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == followupWorkflowInstanceId, ct);
        if (instance is null) return;

        await workflowService.ResumeWorkflowAsync(
            workflowInstanceId: followupWorkflowInstanceId,
            activityId: instance.CurrentActivityId,
            completedBy: "system",
            input: new Dictionary<string, object> { ["decisionTaken"] = "P" },
            cancellationToken: ct);
    }
}
