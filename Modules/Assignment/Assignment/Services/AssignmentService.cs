using Shared.Messaging.Events;

namespace Assignment.Services;

public class AssignmentService(
    IPublishEndpoint publishEndpoint,
    IAssignmentRepository assignmentRepository,
    IAssigneeSelectorFactory selectorFactory,
    IDateTimeProvider dateTimeProvider
) : IAssignmentService
{
    public async Task<Guid> StartWorkflowAsync(long requestId, CancellationToken cancellationToken = default)
    {
        var correlationId = NewId.NextGuid();

        await publishEndpoint.Publish(new RequestSubmitted
            {
                CorrelationId = correlationId,
                RequestId = requestId
            },
            cancellationToken);

        return correlationId;
    }

    public async Task CompleteTaskAsync(Guid correlationId, string taskName, string actionTaken,
        CancellationToken cancellationToken = default)
    {
        var pendingTask =
            await assignmentRepository.GetPendingTaskAsync(correlationId, taskName, cancellationToken);
        if (pendingTask is null)
        {
            throw new NotFoundException(
                $"Pending task with correlation ID {correlationId} and activity name {taskName} not found.");
        }

        var completedTask = CompletedTask.CreateFromPendingTask(pendingTask, actionTaken, dateTimeProvider.Now);

        // TODO: change to unit of work pattern
        await assignmentRepository.RemovePendingTaskAsync(pendingTask, cancellationToken);
        await assignmentRepository.AddCompletedTaskAsync(completedTask, cancellationToken);

        await publishEndpoint.Publish(new TaskCompleted
            {
                CorrelationId = correlationId,
                TaskName = taskName,
                ActionTaken = actionTaken
            },
            cancellationToken);
    }

    public async Task AssignTaskAsync(Guid correlationId, string taskName,
        CancellationToken cancellationToken = default)
    {
        var assigneeSelector = selectorFactory.GetSelector(AssigneeSelectionStrategy.RoundRobin);

        // TODO: Change user groups to be dynamic based on the parameter
        var assignmentContext = new AssignmentContext
        {
            ActivityName = taskName,
            UserGroups = ["Juniors", "Seniors"]
        };

        var result = await assigneeSelector.SelectAssigneeAsync(assignmentContext, cancellationToken);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to select assignee: {result.ErrorMessage}");
        }

        await publishEndpoint.Publish(new TaskAssigned
            {
                CorrelationId = correlationId,
                TaskName = taskName,
                AssignedTo = result.AssigneeId!,
                AssignedType = "U" // Assuming "U" stands for a User type, adjust as necessary
            },
            cancellationToken);
    }
}