using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Schema;
using Assignment.Workflow.Models;

namespace Assignment.Workflow.Activities;

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