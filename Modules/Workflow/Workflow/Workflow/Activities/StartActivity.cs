using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Activities;

public class StartActivity : WorkflowActivityBase
{
    public override string ActivityType => ActivityTypes.StartActivity;
    public override string Name => "Start Activity";
    public override string Description => "Initializes the workflow and sets up initial context";

    protected override async Task<ActivityResult> ExecuteActivityAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        // Initialize the workflow context

        return ActivityResult.Success();
    }
}