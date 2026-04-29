using Appraisal.Contracts.Services;
using WorkflowGroupService = global::Workflow.Services.Groups.IUserGroupService;

namespace Workflow.Tasks.Services;

/// <summary>
/// Exposes IUserGroupService (Workflow) as Appraisal.Contracts.Services.IUserGroupService
/// so the Appraisal module's query handlers can consume it without a direct Workflow reference.
/// </summary>
internal sealed class UserGroupServiceAdapter(WorkflowGroupService inner)
    : Appraisal.Contracts.Services.IUserGroupService
{
    public Task<List<string>> GetGroupsForUserAsync(string username, CancellationToken cancellationToken = default)
        => inner.GetGroupsForUserAsync(username, cancellationToken);
}
