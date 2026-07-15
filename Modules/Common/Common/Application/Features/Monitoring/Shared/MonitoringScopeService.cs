using Dapper;
using Shared.Identity;

namespace Common.Application.Features.Monitoring.Shared;

/// <summary>
/// The set of activity IDs the current user may see on a monitoring screen, split by scope:
///   <see cref="AllActivityIds"/> — layers granted WITHOUT the :TEAM modifier (see every staff).
///   <see cref="TeamActivityIds"/> — layers granted WITH :TEAM (see only the monitor's teammates).
/// An activity granted both ways resolves to "all" (it is removed from the team set).
/// </summary>
public sealed record MonitoringScope(string[] AllActivityIds, string[] TeamActivityIds)
{
    public bool IsEmpty => AllActivityIds.Length == 0 && TeamActivityIds.Length == 0;
}

/// <summary>
/// Determines which workflow activity IDs the current user may see on each monitoring screen.
///
/// Logic:
///   For Internal/External screens the user holds one or more layer permissions
///   (e.g. "MONITORING:PENDING_INTERNAL:CHECKER"). The suffix after the screen prefix
///   is the map key in <see cref="MonitoringActivityMap"/>.
///   Each layer is a flat set — ADMIN is NOT special-cased to "see everything"; it has its
///   own activity-ID list like every other layer (currently scoped to the assignment stage).
///   A user who needs cross-layer visibility must be granted multiple layer permissions.
///
///   A layer permission may carry an optional trailing ":TEAM" modifier
///   (e.g. "MONITORING:PENDING_INTERNAL:CHECKER:TEAM"). Without it the monitor sees every
///   staff's tasks in that stage; with it the monitor is confined to their own "team" as
///   resolved by <see cref="TeamScopePredicate"/> — same auth.Teams team for internal users,
///   same company for external users. The two can be mixed across layers.
///
///   For the 4 admin-only screens (Quotation, Followup, Evaluation, MeetingFollowup)
///   there are no activity-ID layers — the handler skips this service.
/// </summary>
public class MonitoringScopeService(ICurrentUserService currentUserService)
{
    // Marks the optional scope modifier on a layer permission suffix (LAYER:TEAM).
    private const string TeamModifier = "TEAM";

    /// <summary>Resolves the activity scope for the Pending Internal screen.</summary>
    public MonitoringScope ResolveInternalScope()
        => ResolveScope(MonitoringPermissions.PendingInternalPrefix, MonitoringActivityMap.Internal);

    /// <summary>Resolves the activity scope for the Pending External screen.</summary>
    public MonitoringScope ResolveExternalScope()
        => ResolveScope(MonitoringPermissions.PendingExternalPrefix, MonitoringActivityMap.External);

    /// <summary>
    /// Builds the activity-scope WHERE fragment for the given scope and appends it to
    /// <paramref name="conditions"/>, registering any required parameters. Returns false when
    /// the user has no visible activities (caller should short-circuit to an empty result).
    /// </summary>
    public bool TryBuildActivityFilter(MonitoringScope scope, List<string> conditions, DynamicParameters parameters)
    {
        var clause = BuildActivityScopeSql(scope, parameters);
        if (clause is null)
            return false;
        conditions.Add(clause);
        return true;
    }

    /// <summary>
    /// Returns a parenthesized WHERE fragment scoping rows to the user's activities (all-scope
    /// OR team-scoped), or null when the scope is empty. Parameter names are suffixed with
    /// <paramref name="paramSuffix"/> so multiple scopes can coexist in one query (e.g. the
    /// Internal and External branches of the top-breaches UNION).
    /// </summary>
    public string? BuildActivityScopeSql(MonitoringScope scope, DynamicParameters parameters, string paramSuffix = "")
    {
        var parts = new List<string>();

        if (scope.AllActivityIds.Length > 0)
        {
            var p = $"All{paramSuffix}ActivityIds";
            parts.Add($"ActivityId IN @{p}");
            parameters.Add(p, scope.AllActivityIds);
        }

        // Team-scoped layers are gated by the shared team predicate: same auth.Teams team for
        // internal users, same company for external users. The builder fails closed (1 = 0) when
        // the identity value needed to resolve the boundary is missing, so no additional guard
        // is required here.
        if (scope.TeamActivityIds.Length > 0)
        {
            var p = $"Team{paramSuffix}ActivityIds";
            var teamPredicate = TeamScopePredicate.Build(currentUserService, parameters);
            parts.Add($"(ActivityId IN @{p} AND {teamPredicate})");
            parameters.Add(p, scope.TeamActivityIds);
        }

        return parts.Count == 0 ? null : "(" + string.Join(" OR ", parts) + ")";
    }

    private MonitoringScope ResolveScope(
        string permissionPrefix,
        IReadOnlyDictionary<string, string[]> activityMap)
    {
        var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var team = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var permission in currentUserService.Permissions)
        {
            if (!permission.StartsWith(permissionPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            // Suffix after the screen prefix, e.g. "CHECKER" or "CHECKER:TEAM".
            var segments = permission[permissionPrefix.Length..].Split(':');
            var layer = segments[0];
            if (!activityMap.TryGetValue(layer, out var ids))
                continue;

            var teamScoped = segments.Skip(1)
                .Any(s => s.Equals(TeamModifier, StringComparison.OrdinalIgnoreCase));
            var target = teamScoped ? team : all;
            foreach (var id in ids)
                target.Add(id);
        }

        // "All wins": an activity visible globally is never also team-restricted.
        team.ExceptWith(all);

        return new MonitoringScope([.. all], [.. team]);
    }
}
