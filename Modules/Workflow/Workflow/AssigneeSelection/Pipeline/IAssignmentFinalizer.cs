using Workflow.Workflow.Activities.Core;

namespace Workflow.AssigneeSelection.Pipeline;

public interface IAssignmentFinalizer
{
    Task<AssignmentResult> FinalizeAsync(AssignmentPipelineContext context, CancellationToken cancellationToken = default);
}
