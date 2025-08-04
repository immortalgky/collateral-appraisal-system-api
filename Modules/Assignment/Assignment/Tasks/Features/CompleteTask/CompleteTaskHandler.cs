namespace Assignment.Tasks.Features.CompleteTask;

public record CompleteActivityCommand(Guid CorrelationId, string ActivityName, string ActionTaken)
    : ICommand<CompleteActivityResult>;

public record CompleteActivityResult(bool IsSuccess);

public class CompleteTaskHandler(IAssignmentService assignmentService)
    : ICommandHandler<CompleteActivityCommand, CompleteActivityResult>
{
    public async Task<CompleteActivityResult> Handle(CompleteActivityCommand command,
        CancellationToken cancellationToken)
    {
        await assignmentService.CompleteTaskAsync(command.CorrelationId, command.ActivityName, command.ActionTaken,
            cancellationToken);

        return new CompleteActivityResult(true);
    }
}