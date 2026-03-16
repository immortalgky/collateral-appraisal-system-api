using Workflow.Workflow.Activities.Core;

namespace Workflow.AssigneeSelection.Pipeline;

public interface IAssignmentPipeline
{
    Task<AssignmentResult> AssignAsync(ActivityContext context, CancellationToken cancellationToken = default);
    Task<AssignmentPipelineContext> GetEligibleAssigneesAsync(ActivityContext context, CancellationToken cancellationToken = default);
}
