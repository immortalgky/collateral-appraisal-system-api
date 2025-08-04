using Shared.Messaging.Events;

namespace Assignment.EventHandlers;

public class TransitionCompletedEventHandler(
    IDateTimeProvider dateTimeProvider,
    IAssignmentRepository assignmentRepository
) : IConsumer<TransitionCompleted>
{
    public async Task Consume(ConsumeContext<TransitionCompleted> context)
    {
        var pendingTask = PendingTask.Create(
            context.Message.CorrelationId,
            context.Message.RequestId,
            context.Message.TaskName,
            context.Message.AssignedTo,
            "U", // Assuming "U" stands for a User type, adjust as necessary
            dateTimeProvider.Now
        );

        await assignmentRepository.AddTaskAsync(pendingTask);
    }
}