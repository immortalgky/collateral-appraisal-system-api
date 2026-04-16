using MediatR;
using Shared.Identity;
using Workflow.DocumentFollowups.Domain;
using Workflow.Workflow.Services;

namespace Workflow.DocumentFollowups.Application.Commands;

public class SubmitDocumentFollowupCommandHandler(
    WorkflowDbContext dbContext,
    IWorkflowService workflowService,
    ICurrentUserService currentUser,
    IPublisher publisher,
    ILogger<SubmitDocumentFollowupCommandHandler> logger
) : ICommandHandler<SubmitDocumentFollowupCommand, Unit>
{
    public async Task<Unit> Handle(SubmitDocumentFollowupCommand command, CancellationToken cancellationToken)
    {
        var followup = await dbContext.DocumentFollowups
            .FirstOrDefaultAsync(f => f.Id == command.FollowupId, cancellationToken)
            ?? throw new InvalidOperationException($"Document followup {command.FollowupId} not found");

        var actor = currentUser.UserId?.ToString() ?? currentUser.Username
            ?? throw new InvalidOperationException("User not authenticated");

        // Authorization: only the assignee of the followup child workflow task can submit.
        // Fail closed: if not yet provisioned, reject.
        if (!followup.FollowupWorkflowInstanceId.HasValue)
            throw new InvalidOperationException("Followup not fully provisioned");

        var fwInstance = await dbContext.WorkflowInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == followup.FollowupWorkflowInstanceId.Value, cancellationToken)
            ?? throw new InvalidOperationException("Followup workflow instance not found");

        if (!string.Equals(fwInstance.StartedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(
                "Only the request maker assigned to this followup can submit it");

        followup.Submit(actor);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var ev in followup.ClearDomainEvents())
            await publisher.Publish(ev, cancellationToken);

        // Single path back to the raiser — always signal "P" (proceed) regardless of
        // whether items were Uploaded or Declined.
        await workflowService.ResumeWorkflowAsync(
            workflowInstanceId: followup.FollowupWorkflowInstanceId.Value,
            activityId: fwInstance.CurrentActivityId,
            completedBy: actor,
            input: new Dictionary<string, object> { ["decisionTaken"] = "P" },
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Document followup {FollowupId} submitted by {Actor}", command.FollowupId, actor);
        return Unit.Value;
    }
}
