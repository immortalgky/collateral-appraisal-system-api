using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Contracts.DocumentFollowups;
using Workflow.Data;

namespace Workflow.EventHandlers;

/// <summary>
/// Consumes RequestResubmittedIntegrationEvent published by the Integration module's
/// resubmit endpoint. The consumer runs in Workflow's own DbContext scope, so the followup
/// fulfill + workflow resume happen inside a Workflow-side transaction. Combined with the
/// upstream persistent outbox in the Request module, this gives end-to-end atomicity:
/// if the Request transaction rolls back, the outbox row never exists and this consumer
/// never runs; if it commits, MassTransit retries cover transient consumer failures and
/// InboxGuard dedupes redelivery.
///
/// Branches on FollowupId:
///   - null  → ResumeParentWorkflowForRequestCommand (parent appraisal workflow at appraisal-initiation)
///   - set   → AutoResolveDocumentFollowupCommand (auto-resolves followup; no per-item coverage check)
/// </summary>
public class RequestResubmittedIntegrationEventConsumer(
    ISender mediator,
    InboxGuard<WorkflowDbContext> inboxGuard,
    ILogger<RequestResubmittedIntegrationEventConsumer> logger)
    : IConsumer<RequestResubmittedIntegrationEvent>
{
    private const string SystemActor = "system:integration";

    public async Task Consume(ConsumeContext<RequestResubmittedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for RequestId: {RequestId}, FollowupId: {FollowupId}",
            nameof(RequestResubmittedIntegrationEvent), message.RequestId, message.FollowupId);

        try
        {
            if (message.FollowupId.HasValue)
            {
                await mediator.Send(
                    new AutoResolveDocumentFollowupCommand(
                        message.FollowupId.Value,
                        SystemActor,
                        "Auto-resolved via integration API resubmit"),
                    ct);
            }
            else
            {
                await mediator.Send(
                    new ResumeParentWorkflowForRequestCommand(message.RequestId, SystemActor),
                    ct);
            }

            await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing RequestResubmittedIntegrationEvent for RequestId: {RequestId}, FollowupId: {FollowupId}",
                message.RequestId, message.FollowupId);
            throw;
        }
    }
}
