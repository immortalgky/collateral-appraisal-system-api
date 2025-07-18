using Elsa.Extensions;
using Elsa.Workflows.Activities.Flowchart.Attributes;
using Elsa.Workflows.Helpers;
using Elsa.Workflows.Memory;
using Elsa.Workflows.Models;
using Elsa.Workflows.Runtime.Options;
using Microsoft.Extensions.Logging;
using Task.Data.Repository;
using Task.Tasks.Models;

namespace Workflow.Workflow.Activities;

[Activity("UserTask", Category = "Workflow",
    Description = "A user task activity for handling user-specific tasks in workflows.")]
public class UserTask : Activity
{
    public Input<string> TaskName { get; set; } = default!;
    public Input<string> AssignedTo { get; set; } = default!;
    public Input<string> AssignedType { get; set; } = default!;
    public Output<string> ActionTaken { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<UserTask>>();

        var taskRepository = context.GetRequiredService<ITaskRepository>();

        context.CreateBookmark(new UserTaskBookmark
        {
            TaskName = TaskName.Get(context),
        }, OnResume, false);

        var correlationId = context.WorkflowExecutionContext.CorrelationId;

        var task = CreateNewTask(TaskName.Get(context), AssignedTo.Get(context), AssignedType.Get(context), correlationId);

        await taskRepository.AddTaskAsync(task, context.CancellationToken);
    }

    private async ValueTask OnResume(ActivityExecutionContext context)
    {
        ActionTaken.Set(context, context.GetWorkflowInput<string>("ActionTaken"));
        await context.CompleteActivityAsync();
    }

    private static Task.Tasks.Models.Task CreateNewTask(string taskName, string assignedTo, string assignedType, string correlationId)
    {
        var task = Task.Tasks.Models.Task.Create(taskName, assignedTo, assignedType, correlationId);

        return task;
    }
}

public class UserTaskBookmark
{
    public string TaskId { get; set; } = default!;
    public string TaskName { get; set; } = default!;
    public string AssignedTo { get; set; } = default!;
    public string AssignedType { get; set; } = default!;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public enum UserTaskAssignedType
{
    User = 'U',
    Group = 'G'
}