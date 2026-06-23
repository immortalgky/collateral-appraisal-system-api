
public class SaveDecisionCommandHandler(
WorkflowDbContext dbContext
) : ICommandHandler<SaveDecisionCommand, SaveDecisionResult>
{
    public async Task<SaveDecisionResult> Handle(SaveDecisionCommand command, CancellationToken cancellationToken)
    {
        var task = await dbContext.PendingTasks.FindAsync(command.TaskId, cancellationToken);
        if (task is null)
            return new SaveDecisionResult(false, ErrorMessage: "Task not found");

        task.Update(command.DecisionType, command.AssignNextToType, command.CommentDecision);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new SaveDecisionResult(true);
    }
}