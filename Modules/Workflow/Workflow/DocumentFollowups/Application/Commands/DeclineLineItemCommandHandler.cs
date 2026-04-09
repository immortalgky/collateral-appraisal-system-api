using MassTransit;
using Shared.Identity;
using Shared.Messaging.Events;
using Workflow.DocumentFollowups.Domain;

namespace Workflow.DocumentFollowups.Application.Commands;

public class DeclineLineItemCommandHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUser,
    IPublisher publisher,
    IPublishEndpoint publishEndpoint,
    ILogger<DeclineLineItemCommandHandler> logger
) : ICommandHandler<DeclineLineItemCommand, Unit>
{
    public async Task<Unit> Handle(DeclineLineItemCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ArgumentException("Reason is required to decline a line item");

        var followup = await dbContext.DocumentFollowups
            .FirstOrDefaultAsync(f => f.Id == command.FollowupId, cancellationToken)
            ?? throw new InvalidOperationException($"Document followup {command.FollowupId} not found");

        if (followup.Status != DocumentFollowupStatus.Open)
            throw new InvalidOperationException("Followup is not open");

        var actor = currentUser.UserId?.ToString() ?? currentUser.Username
            ?? throw new InvalidOperationException("User not authenticated");

        // Fail closed: if the followup workflow has not yet been attached (race with Raise
        // handler), we cannot validate authorization. Reject rather than silently permitting
        // any authenticated user to decline.
        if (!followup.FollowupWorkflowInstanceId.HasValue)
            throw new InvalidOperationException("Followup not fully provisioned");

        var fwInstance = await dbContext.WorkflowInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == followup.FollowupWorkflowInstanceId.Value, cancellationToken)
            ?? throw new InvalidOperationException("Followup workflow instance not found");

        if (!string.Equals(fwInstance.StartedBy, actor, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                "Only the request maker assigned to this followup can decline line items");
        }

        followup.DeclineLineItem(command.LineItemId, command.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var ev in followup.ClearDomainEvents())
            await publisher.Publish(ev, cancellationToken);

        // Notify the raising checker that a line item was declined. This fires regardless of
        // whether the followup auto-resolved (the resolved handler emits a separate event).
        await publishEndpoint.Publish(new DocumentFollowupNotificationIntegrationEvent
        {
            Type = "DocumentLineItemDeclined",
            FollowupId = followup.Id,
            RaisingTaskId = followup.RaisingPendingTaskId,
            ParentAppraisalId = followup.AppraisalId,
            FollowupWorkflowInstanceId = followup.FollowupWorkflowInstanceId,
            Recipient = followup.RaisingUserId,
            Title = "Document Declined",
            Message = "A requested document was declined by the request maker.",
            Reason = command.Reason
        }, cancellationToken);

        logger.LogInformation(
            "Declined line item {LineItemId} on followup {FollowupId} by {Actor}",
            command.LineItemId, command.FollowupId, actor);
        return Unit.Value;
    }
}
