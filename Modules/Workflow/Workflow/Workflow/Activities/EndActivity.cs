using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Activities;

public class EndActivity : WorkflowActivityBase
{
    public override string ActivityType => ActivityTypes.EndActivity;
    public override string Name => "End Activity";
    public override string Description => "Marks the end of a workflow instance";

    protected override async Task<ActivityResult> ExecuteActivityAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        // Clean up resources or perform any final actions before ending the workflow
        // This could include logging, notifying other systems, etc.

        return ActivityResult.Success();
    }
}