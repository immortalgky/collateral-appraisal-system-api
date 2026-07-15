using Dapper;

namespace Shared.Identity;

/// <summary>
/// Builds the SQL predicate that restricts a person-assignee column to the current user's
/// "team" — the single source of truth shared by every screen that offers a <c>:TEAM</c>
/// permission scope (Monitoring pending screens, Task Monitor).
///
/// "Team" is resolved by user kind:
///   • Internal (bank) user  → members of the same <c>auth.Teams</c> team(s) as the user
///                             (multi-team membership = UNION of all their teams' members).
///   • External (company) user → users belonging to the same <c>CompanyId</c>. External appraisal
///                             companies are not modelled as <c>auth.Teams</c> rows, so the company
///                             itself acts as the team boundary.
///
/// The predicate only ever matches person-assigned rows (<c>AssignedType = '1'</c>); pool/group
/// rows never match and are intentionally excluded from team-restricted scopes.
///
/// Fail-closed: if the identity value needed to resolve the boundary (Username for internal,
/// CompanyId for external) is missing, the predicate is <c>1 = 0</c> — a scoped user sees nothing
/// rather than everything.
/// </summary>
public static class TeamScopePredicate
{
    /// <summary>
    /// Returns a parenthesised SQL predicate on <paramref name="assignedToCol"/> and registers the
    /// parameter it needs on <paramref name="p"/>. The identity parameter (<c>@MeNorm</c> /
    /// <c>@ScopeCompanyId</c>) is shared and registered idempotently — the boundary always resolves
    /// against the same current user, so multiple scopes in one query (e.g. the Monitoring
    /// top-breaches UNION) reuse a single parameter. Never returns null: on the fail-closed path it
    /// returns <c>1 = 0</c>, so a caller that ANDs this fragment restricts to nothing rather than
    /// leaking every row.
    ///
    /// <paramref name="requirePersonAssigned"/> prepends an <c>AssignedType = '1'</c> guard so
    /// pool/group rows never match. Set it to false when the source already exposes only
    /// person-assigned rows AND has no <c>AssignedType</c> column (e.g. <c>workflow.vw_TaskMonitor</c>),
    /// otherwise the guard references a non-existent column.
    /// </summary>
    public static string Build(
        ICurrentUserService user,
        DynamicParameters p,
        string assignedToCol = "AssignedTo",
        bool requirePersonAssigned = true)
    {
        var guard = requirePersonAssigned ? "AssignedType = '1' AND " : "";

        if (user.IsExternal)
        {
            var companyId = user.CompanyId;
            if (companyId is null)
                return "1 = 0";

            if (!p.ParameterNames.Contains("ScopeCompanyId"))
                p.Add("ScopeCompanyId", companyId.Value);

            return $"""
                ({guard}UPPER({assignedToCol}) IN (
                    SELECT u.NormalizedUserName
                    FROM auth.AspNetUsers u
                    WHERE u.CompanyId = @ScopeCompanyId
                ))
                """;
        }

        var meNorm = user.Username?.ToUpperInvariant();
        if (string.IsNullOrEmpty(meNorm))
            return "1 = 0";

        if (!p.ParameterNames.Contains("MeNorm"))
            p.Add("MeNorm", meNorm);

        // Internal: teammates via auth.TeamMembers/auth.Teams. AssignedTo is compared via UPPER()
        // because the source stores it raw while auth.NormalizedUserName is uppercase.
        return $"""
            ({guard}UPPER({assignedToCol}) IN (
                SELECT tmateU.NormalizedUserName
                FROM auth.TeamMembers myTm
                INNER JOIN auth.AspNetUsers me ON me.Id = myTm.UserId AND me.NormalizedUserName = @MeNorm
                INNER JOIN auth.Teams t ON t.Id = myTm.TeamId
                INNER JOIN auth.TeamMembers tmate ON tmate.TeamId = t.Id
                INNER JOIN auth.AspNetUsers tmateU ON tmateU.Id = tmate.UserId
            ))
            """;
    }
}
