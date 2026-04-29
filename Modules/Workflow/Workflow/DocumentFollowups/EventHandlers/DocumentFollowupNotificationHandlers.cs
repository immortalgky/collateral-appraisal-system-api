using MassTransit;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Workflow.DocumentFollowups.Domain;
using Workflow.DocumentFollowups.Domain.Events;

namespace Workflow.DocumentFollowups.EventHandlers;

/// <summary>
/// Translates in-process domain events raised by the <see cref="DocumentFollowup"/>
/// aggregate into SignalR-bound integration events consumed by the Notification module.
/// Handlers are idempotent — republished events simply re-send a notification, which the
/// frontend de-duplicates on <c>FollowupId</c>.
/// </summary>
public class DocumentFollowupRaisedNotificationHandler(
    WorkflowDbContext dbContext,
    IPublishEndpoint publishEndpoint,
    IIntegrationEventOutbox outbox,
    ILogger<DocumentFollowupRaisedNotificationHandler> logger)
    : INotificationHandler<DocumentFollowupRaisedDomainEvent>
{
    public async Task Handle(DocumentFollowupRaisedDomainEvent notification, CancellationToken cancellationToken)
    {
        var followup = await dbContext.DocumentFollowups
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == notification.FollowupId, cancellationToken);
        if (followup is null)
        {
            logger.LogWarning("DocumentFollowupRaisedNotificationHandler: followup {FollowupId} not found", notification.FollowupId);
            return;
        }

        // Recipient = StartedBy of the followup workflow instance (i.e. the request maker).
        // Until the followup workflow is attached, fall back to the parent workflow's StartedBy
        // so the notification still fires during the Raise transaction.
        var recipient = await ResolveRequestMakerAsync(followup, cancellationToken);
        if (string.IsNullOrWhiteSpace(recipient))
        {
            logger.LogWarning(
                "DocumentFollowupRaisedNotificationHandler: no recipient resolved for followup {FollowupId}",
                followup.Id);
            return;
        }

        await publishEndpoint.Publish(new DocumentFollowupNotificationIntegrationEvent
        {
            Type = "DocumentFollowupRaised",
            FollowupId = followup.Id,
            RaisingTaskId = followup.RaisingPendingTaskId,
            ParentAppraisalId = followup.AppraisalId,
            FollowupWorkflowInstanceId = followup.FollowupWorkflowInstanceId,
            Recipient = recipient,
            Title = "Additional Documents Requested",
            Message = $"A checker has requested {followup.LineItems.Count} additional document(s)."
        }, cancellationToken);

        // Also publish thin outbound event for the external webhook bridge.
        // ReasonCode = the activity that raised the followup; Reason = document types requested.
        var documentTypes = string.Join(", ", followup.LineItems.Select(li => li.DocumentType));
        outbox.Publish(new DocumentFollowupRequiredIntegrationEvent
        {
            AppraisalId = followup.AppraisalId,
            FollowupId = followup.Id,
            ReasonCode = followup.RaisingActivityId,
            Reason = $"Additional documents requested: {documentTypes}"
        }, correlationId: followup.AppraisalId.ToString());
    }

    private async Task<string?> ResolveRequestMakerAsync(DocumentFollowup followup, CancellationToken ct)
    {
        if (followup.FollowupWorkflowInstanceId.HasValue)
        {
            var fw = await dbContext.WorkflowInstances
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == followup.FollowupWorkflowInstanceId.Value, ct);
            if (!string.IsNullOrWhiteSpace(fw?.StartedBy))
                return fw.StartedBy;
        }

        var parent = await dbContext.WorkflowInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == followup.RaisingWorkflowInstanceId, ct);
        return parent?.StartedBy;
    }
}

public class DocumentFollowupResolvedNotificationHandler(
    WorkflowDbContext dbContext,
    IPublishEndpoint publishEndpoint,
    ILogger<DocumentFollowupResolvedNotificationHandler> logger)
    : INotificationHandler<DocumentFollowupResolvedDomainEvent>
{
    public async Task Handle(DocumentFollowupResolvedDomainEvent notification, CancellationToken cancellationToken)
    {
        var followup = await dbContext.DocumentFollowups
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == notification.FollowupId, cancellationToken);
        if (followup is null)
        {
            logger.LogWarning("DocumentFollowupResolvedNotificationHandler: followup {FollowupId} not found", notification.FollowupId);
            return;
        }

        await publishEndpoint.Publish(new DocumentFollowupNotificationIntegrationEvent
        {
            Type = "DocumentFollowupResolved",
            FollowupId = followup.Id,
            RaisingTaskId = followup.RaisingPendingTaskId,
            ParentAppraisalId = followup.AppraisalId,
            FollowupWorkflowInstanceId = followup.FollowupWorkflowInstanceId,
            Recipient = followup.RaisingUserId,
            Title = "Document Followup Resolved",
            Message = "All requested documents have been provided or declined."
        }, cancellationToken);
    }
}

public class DocumentFollowupCancelledNotificationHandler(
    WorkflowDbContext dbContext,
    IPublishEndpoint publishEndpoint,
    ILogger<DocumentFollowupCancelledNotificationHandler> logger)
    : INotificationHandler<DocumentFollowupCancelledDomainEvent>
{
    public async Task Handle(DocumentFollowupCancelledDomainEvent notification, CancellationToken cancellationToken)
    {
        var followup = await dbContext.DocumentFollowups
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == notification.FollowupId, cancellationToken);
        if (followup is null)
        {
            logger.LogWarning("DocumentFollowupCancelledNotificationHandler: followup {FollowupId} not found", notification.FollowupId);
            return;
        }

        await publishEndpoint.Publish(new DocumentFollowupNotificationIntegrationEvent
        {
            Type = "DocumentFollowupCancelled",
            FollowupId = followup.Id,
            RaisingTaskId = followup.RaisingPendingTaskId,
            ParentAppraisalId = followup.AppraisalId,
            FollowupWorkflowInstanceId = followup.FollowupWorkflowInstanceId,
            Recipient = followup.RaisingUserId,
            Title = "Document Followup Cancelled",
            Message = "The document followup was cancelled.",
            Reason = notification.Reason
        }, cancellationToken);
    }
}
