namespace Workflow.AssigneeSelection.Pipeline;

public interface IAssignmentContextBuilder
{
    Task BuildAsync(AssignmentPipelineContext context, CancellationToken cancellationToken = default);
}
