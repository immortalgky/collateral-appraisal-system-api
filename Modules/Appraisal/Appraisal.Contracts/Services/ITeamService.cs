namespace Appraisal.Contracts.Services;

/// <summary>
/// Cross-module contract: returns team membership for a user.
/// Implemented in the Workflow module; consumed by Appraisal query handlers
/// that build pool-task visibility clauses.
/// </summary>
public interface ITeamService
{
    /// <summary>Returns the team the user belongs to, or null if unassigned.</summary>
    Task<TeamInfo?> GetTeamForUserAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>Minimal team descriptor needed to build pool-task SQL clauses.</summary>
public record TeamInfo(string TeamId, string Name);
