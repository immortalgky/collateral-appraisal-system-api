using Workflow.AssigneeSelection.Teams;

namespace Workflow.AssigneeSelection.Pipeline;

public interface IAssignmentFilter
{
    int Order { get; }
    Task<List<TeamMemberInfo>> FilterAsync(AssignmentPipelineContext context, List<TeamMemberInfo> candidates, CancellationToken cancellationToken = default);
}
