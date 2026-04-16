using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.DocumentFollowups.Domain;

namespace Workflow.DocumentFollowups.EventHandlers;

/// <summary>
/// Listens for document uploads against a Request and fulfills the first matching
/// open document followup line item (flips it to Uploaded).
/// Does NOT resolve the followup — the request maker must explicitly Submit.
/// </summary>
public class RequestDocumentLinkedConsumer(
    WorkflowDbContext dbContext,
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
                "Fulfilled line item {LineItemId} on followup {FollowupId} via document {DocumentId}. " +
                "Followup stays Open until request maker submits.",
                matchedLineItemId, followup.Id, msg.DocumentId);

            // First match wins per upload — stop scanning further followups.
            break;
        }

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, context.CancellationToken);
    }
}
