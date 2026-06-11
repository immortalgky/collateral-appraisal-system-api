using Shared.Exceptions;

namespace Auth.Application.Services;

/// <summary>
/// Shared validation for assigning a user to groups/teams. Used by user creation and the
/// dedicated group/team update handlers so the "all requested ids must exist" rule lives in one
/// place (otherwise the same block drifts across three handlers).
/// </summary>
public static class UserAssignmentValidator
{
    public static async Task ValidateGroupsExistAsync(
        AuthDbContext db, IReadOnlyCollection<Guid> groupIds, CancellationToken ct)
    {
        if (groupIds.Count == 0) return;

        var existing = await db.Groups
            .Where(g => groupIds.Contains(g.Id))
            .Select(g => g.Id)
            .ToListAsync(ct);

        var missing = groupIds.Except(existing).ToList();
        if (missing.Count > 0) throw new NotFoundException("Group", missing[0]);
    }

    public static async Task ValidateTeamsExistAsync(
        AuthDbContext db, IReadOnlyCollection<Guid> teamIds, CancellationToken ct)
    {
        if (teamIds.Count == 0) return;

        var existing = await db.Teams
            .Where(t => teamIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync(ct);

        var missing = teamIds.Except(existing).ToList();
        if (missing.Count > 0) throw new NotFoundException("Team", missing[0]);
    }
}
