using Shared.Messaging.Commands;
using Shared.Messaging.Events;

namespace Assignment.EventHandlers;

public class TransitionCompletedEventHandler(
    IDateTimeProvider dateTimeProvider,
    IAssignmentRepository assignmentRepository
) : IConsumer<TransitionCompleted>
{
    public async Task Consume(ConsumeContext<TransitionCompleted> context)
    {
        await NotifyAssignment(context);

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

    private async Task NotifyAssignment(ConsumeContext<TransitionCompleted> context)
    {
        var task = await assignmentRepository
            .GetLastCompletedTaskForIdAsync(
                context.Message.CorrelationId
                );
        if (task != null)
        {
            var notifiedTo = task.AssignedTo;
            var endpoint = await context.GetSendEndpoint(new Uri("queue:notify-assignment-command-handler"));
            await endpoint.Send(new NotifyAssignment
            {
                CorrelationId = context.Message.CorrelationId,
                TaskName = context.Message.TaskName,
                AssignedTo = context.Message.AssignedTo,
                AssignedType = context.Message.AssignedType,
                NotifiedTo = notifiedTo
            });
        }
    }
}