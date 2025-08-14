using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Schema;

namespace Assignment.Workflow.Activities;

public class StartActivity : WorkflowActivityBase
{
    public override string ActivityType => ActivityTypes.StartActivity;
    public override string Name => "Start Activity";
    public override string Description => "Initializes the workflow and sets up initial context";

    public override async Task<ActivityResult> ExecuteAsync(ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        // Initialize the workflow context

        return ActivityResult.Success();
    }
}