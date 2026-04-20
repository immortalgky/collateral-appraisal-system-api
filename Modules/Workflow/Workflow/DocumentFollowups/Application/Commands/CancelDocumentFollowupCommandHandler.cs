using Shared.Identity;
using Workflow.DocumentFollowups.Domain;
using Workflow.Workflow.Services;

namespace Workflow.DocumentFollowups.Application.Commands;

public class CancelDocumentFollowupCommandHandler(
    WorkflowDbContext dbContext,
    IWorkflowService workflowService,
    ICurrentUserService currentUser,
    ILogger<CancelDocumentFollowupCommandHandler> logger
) : ICommandHandler<CancelDocumentFollowupCommand, Unit>
{
    public async Task<Unit> Handle(CancelDocumentFollowupCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ArgumentException("Reason is required to cancel a document followup");

        var followup = await dbContext.DocumentFollowups
                           .FirstOrDefaultAsync(f => f.Id == command.FollowupId, cancellationToken)
                       ?? throw new InvalidOperationException($"Document followup {command.FollowupId} not found");

        if (followup.Status != DocumentFollowupStatus.Open)
            throw new InvalidOperationException("Followup is not open");

        // Authorization: only the raising user can cancel.
        var actor = currentUser.Username
                    ?? throw new InvalidOperationException("User not authenticated");
        if (!string.Equals(actor, followup.RaisingUserId, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Only the raising user can cancel this followup");

        followup.Cancel(command.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Cancel the followup workflow so the request maker's task is removed from their inbox.
        if (followup.FollowupWorkflowInstanceId.HasValue)
            await workflowService.CancelWorkflowAsync(
                followup.FollowupWorkflowInstanceId.Value,
                actor,
                command.Reason,
                cancellationToken);

        logger.LogInformation("Cancelled document followup {FollowupId} by {Actor}", command.FollowupId, actor);
        return Unit.Value;
    }
}

public class CancelDocumentFollowupLineItemCommandHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUser,
    IPublisher publisher,
    ILogger<CancelDocumentFollowupLineItemCommandHandler> logger
) : ICommandHandler<CancelDocumentFollowupLineItemCommand, Unit>
{
    public async Task<Unit> Handle(CancelDocumentFollowupLineItemCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ArgumentException("Reason is required to cancel a line item");

        var followup = await dbContext.DocumentFollowups
                           .FirstOrDefaultAsync(f => f.Id == command.FollowupId, cancellationToken)
                       ?? throw new InvalidOperationException($"Document followup {command.FollowupId} not found");

        var actor = currentUser.Username
                    ?? throw new InvalidOperationException("User not authenticated");
        if (!string.Equals(actor, followup.RaisingUserId, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Only the raising user can cancel line items on this followup");

        followup.CancelLineItem(command.LineItemId, command.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Publish any domain events the aggregate raised (e.g. resolved when last item closed)
        foreach (var ev in followup.ClearDomainEvents())
            await publisher.Publish(ev, cancellationToken);

        logger.LogInformation(
            "Cancelled line item {LineItemId} on followup {FollowupId} by {Actor}",
            command.LineItemId, command.FollowupId, actor);
        return Unit.Value;
    }
}