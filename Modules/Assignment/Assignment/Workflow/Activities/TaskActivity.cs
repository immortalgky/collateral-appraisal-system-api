using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Schema;

namespace Assignment.Workflow.Activities;

public class TaskActivity : WorkflowActivityBase
{
    public override string ActivityType => ActivityTypes.TaskActivity;
    public override string Name => "Task Activity";
    public override string Description => "Assigns a task to a user or role for completion";

    public override async Task<ActivityResult> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var assigneeRole = GetProperty<string>(context, "assigneeRole");
        var formFields = GetProperty<List<string>>(context, "formFields", new List<string>());
        var requiresApproval = GetProperty<bool>(context, "requiresApproval", false);
        var timeoutDuration = GetProperty<TimeSpan?>(context, "timeoutDuration");

        // For task activities, we typically return pending and wait for external completion
        // The actual task assignment would be handled by the workflow engine
        
        var outputData = new Dictionary<string, object>
        {
            ["assigneeRole"] = assigneeRole ?? string.Empty,
            ["formFields"] = formFields,
            ["requiresApproval"] = requiresApproval,
            ["assignedAt"] = DateTime.UtcNow
        };

        if (timeoutDuration.HasValue)
        {
            outputData["timeoutAt"] = DateTime.UtcNow.Add(timeoutDuration.Value);
        }

        return ActivityResult.Pending(outputData: outputData);
    }

    public override Task<Core.ValidationResult> ValidateAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        
        var assigneeRole = GetProperty<string>(context, "assigneeRole");
        if (string.IsNullOrEmpty(assigneeRole))
        {
            errors.Add("AssigneeRole is required for TaskActivity");
        }

        return Task.FromResult(errors.Any() ? Core.ValidationResult.Failure(errors.ToArray()) : Core.ValidationResult.Success());
    }
}