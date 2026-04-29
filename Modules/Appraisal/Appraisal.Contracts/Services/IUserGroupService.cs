namespace Appraisal.Contracts.Services;

/// <summary>
/// Cross-module contract: returns the role-groups a user belongs to.
/// Implemented in the Workflow module; consumed by Appraisal query handlers
/// that build pool-task visibility clauses.
/// </summary>
public interface IUserGroupService
{
    Task<List<string>> GetGroupsForUserAsync(string username, CancellationToken cancellationToken = default);
}
