using Workflow.Workflow.Models;

namespace Workflow.Workflow.Activities.Core;

public interface IWorkflowActivity
{
    string ActivityType { get; }
    string Name { get; }
    string Description { get; }

    Task<ActivityResult> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken = default);

    Task<ActivityResult> ResumeAsync(ActivityContext context, Dictionary<string, object> resumeInput,
        CancellationToken cancellationToken = default);

    Task<ValidationResult> ValidateAsync(ActivityContext context, CancellationToken cancellationToken = default);
}