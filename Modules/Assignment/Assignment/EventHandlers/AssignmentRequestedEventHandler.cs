namespace Assignment.EventHandlers;

public class AssignmentRequestedEventHandler(IAssignmentService assignmentService) : IConsumer<AssignmentRequested>
{
    public async Task Consume(ConsumeContext<AssignmentRequested> context)
    {
        await assignmentService.AssignTaskAsync(
            context.Message.CorrelationId,
            context.Message.TaskName,
            context.CancellationToken
        );
    }
}