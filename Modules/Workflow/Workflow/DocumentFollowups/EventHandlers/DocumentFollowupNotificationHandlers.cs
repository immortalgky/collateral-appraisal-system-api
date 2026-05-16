using Parameter.Contracts.DocumentRequirements;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Workflow.DocumentFollowups.Domain;
using Workflow.DocumentFollowups.Domain.Events;

namespace Workflow.DocumentFollowups.EventHandlers;

/// <summary>
/// Translates in-process domain events raised by the <see cref="DocumentFollowup"/>
/// aggregate into SignalR-bound integration events consumed by the Notification module.
/// Both events are routed through the persistent outbox so they commit atomically with
/// the aggregate write — no phantom notifications if the transaction later rolls back.
/// </summary>
public class DocumentFollowupRaisedNotificationHandler(
    WorkflowDbContext dbContext,
    IIntegrationEventOutbox outbox,
    IDocumentChecklistService checklist,
    ILogger<DocumentFollowupRaisedNotificationHandler> logger)
    : INotificationHandler<DocumentFollowupRaisedDomainEvent>
{
    public async Task Handle(DocumentFollowupRaisedDomainEvent notification, CancellationToken cancellationToken)
    {
        // The DocumentFollowup row is not yet committed at this point — the interceptor runs
        // pre-save. Everything we need is on the event payload; the only DB lookup is the
        // parent WorkflowInstance, which already existed before this request.
        var parent = await dbContext.WorkflowInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == notification.RaisingWorkflowInstanceId, cancellationToken);
        var recipient = parent?.StartedBy;
        if (string.IsNullOrWhiteSpace(recipient))
        {
            logger.LogWarning(
                "DocumentFollowupRaisedNotificationHandler: no recipient resolved for followup {FollowupId}",
                notification.FollowupId);
            return;
        }

        outbox.Publish(new DocumentFollowupNotificationIntegrationEvent
        {
            Type = "DocumentFollowupRaised",
            FollowupId = notification.FollowupId,
            RaisingTaskId = notification.RaisingPendingTaskId,
            ParentAppraisalId = notification.AppraisalId,
            FollowupWorkflowInstanceId = null,
            Recipient = recipient,
            Title = "Additional Documents Requested",
            Message = $"A checker has requested {notification.DocumentTypes.Count} additional document(s)."
        }, correlationId: notification.AppraisalId.ToString());

        var nameMap = await checklist.GetAllDocumentTypeNamesAsync(cancellationToken);
        var displayNames = notification.DocumentTypes
            .Select(code => nameMap.TryGetValue(code.ToUpperInvariant(), out var n) ? n : code)
            .ToList();

        outbox.Publish(new DocumentFollowupRequiredIntegrationEvent
        {
            AppraisalId = notification.AppraisalId,
            FollowupId = notification.FollowupId,
            ReasonCode = "MISSING_DOCUMENT",
            Reason = $"Missing documents: {string.Join(", ", displayNames)}"
        }, correlationId: notification.AppraisalId.ToString());
    }
}

public class DocumentFollowupResolvedNotificationHandler(
    WorkflowDbContext dbContext,
    IIntegrationEventOutbox outbox,
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

        outbox.Publish(new DocumentFollowupNotificationIntegrationEvent
        {
            Type = "DocumentFollowupResolved",
            FollowupId = followup.Id,
            RaisingTaskId = followup.RaisingPendingTaskId,
            ParentAppraisalId = followup.AppraisalId,
            FollowupWorkflowInstanceId = followup.FollowupWorkflowInstanceId,
            Recipient = followup.RaisingUserId,
            Title = "Document Followup Resolved",
            Message = "All requested documents have been provided or declined."
        }, correlationId: followup.AppraisalId.ToString());
    }
}

public class DocumentFollowupCancelledNotificationHandler(
    WorkflowDbContext dbContext,
    IIntegrationEventOutbox outbox,
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

        outbox.Publish(new DocumentFollowupNotificationIntegrationEvent
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
        }, correlationId: followup.AppraisalId.ToString());
    }
}
