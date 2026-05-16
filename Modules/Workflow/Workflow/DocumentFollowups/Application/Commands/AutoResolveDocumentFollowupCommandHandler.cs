using Workflow.Contracts.DocumentFollowups;
using Workflow.Workflow.Services;

namespace Workflow.DocumentFollowups.Application.Commands;

public class AutoResolveDocumentFollowupCommandHandler(
    WorkflowDbContext dbContext,
    IWorkflowService workflowService,
    ILogger<AutoResolveDocumentFollowupCommandHandler> logger
) : ICommandHandler<AutoResolveDocumentFollowupCommand, Unit>
{
    public async Task<Unit> Handle(AutoResolveDocumentFollowupCommand command, CancellationToken cancellationToken)
    {
        var followup = await dbContext.DocumentFollowups
                           .FirstOrDefaultAsync(f => f.Id == command.FollowupId, cancellationToken)
                       ?? throw new InvalidOperationException($"Document followup {command.FollowupId} not found");

        if (!followup.FollowupWorkflowInstanceId.HasValue)
            throw new InvalidOperationException("Followup is not fully provisioned — workflow instance not yet attached");

        var fwInstance = await dbContext.WorkflowInstances
                             .AsNoTracking()
                             .FirstOrDefaultAsync(w => w.Id == followup.FollowupWorkflowInstanceId.Value, cancellationToken)
                         ?? throw new InvalidOperationException("Followup workflow instance not found");

        followup.AutoResolve(command.Actor, command.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);

        await workflowService.ResumeWorkflowAsync(
            followup.FollowupWorkflowInstanceId.Value,
            fwInstance.CurrentActivityId,
            command.Actor,
            new Dictionary<string, object> { ["decisionTaken"] = "P" },
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Document followup {FollowupId} auto-resolved by actor {Actor}",
            command.FollowupId, command.Actor);

        return Unit.Value;
    }
}
