namespace Workflow.Tasks.Authorization;

public static class PoolTaskAccess
{
    /// <summary>Builds the SQL fragment + parameters that gate pool-task visibility for the caller.
    /// Returns null when the caller has no groups AND no username — caller should short-circuit to
    /// an empty result.
    ///
    /// Company matching is driven by the TASK's AssigneeCompanyId, not the caller's:
    ///   - Row has AssigneeCompanyId → caller's CompanyId must match (external company user).
    ///     Internal callers (callerCompanyId is null) cannot match company-scoped rows.
    ///   - Row has AssigneeCompanyId IS NULL → any caller whose group/team matches AssigneeUserId
    ///     passes. This covers team-only assignments such as "ExtAdmin:Team_&lt;guid&gt;" and
    ///     RM direct-assignment tasks.
    ///
    /// Username:
    ///   - When <paramref name="username"/> is provided, it is added to the candidate set so
    ///     direct-assignment tasks (AssignedType='1', AssignedTo=&lt;username&gt;) also match.
    /// </summary>
    public static PoolTaskAccessClause? BuildSqlClause(
        IReadOnlyList<string> userGroups,
        string? userTeamId,
        Guid? callerCompanyId,
        string? username = null)
    {
        var candidates = BuildCandidateSet(userGroups, userTeamId, username);
        if (candidates.Count == 0)
            return null;

        var inParams = new Dictionary<string, object?>(StringComparer.Ordinal);
        var paramNames = new List<string>(candidates.Count);
        var i = 0;
        foreach (var c in candidates)
        {
            var key = $"PoolAssignee{i++}";
            paramNames.Add($"@{key}");
            inParams[key] = c;
        }

        string companySql;
        if (callerCompanyId.HasValue)
        {
            // External caller: match either (a) team-only rows (no company gate) or
            // (b) rows scoped to the caller's company.
            inParams["PoolCallerCompanyId"] = callerCompanyId;
            companySql = "(AssigneeCompanyId IS NULL OR AssigneeCompanyId = @PoolCallerCompanyId)";
        }
        else
        {
            // Internal caller (no company context): only match rows that aren't scoped to a
            // company — anything else belongs to a specific external company.
            companySql = "AssigneeCompanyId IS NULL";
        }

        var sql = $"(AssigneeUserId IN ({string.Join(", ", paramNames)}) AND {companySql})";

        return new PoolTaskAccessClause(sql, inParams);
    }

    /// <summary>In-memory ownership check for a single hydrated row (used by GetTaskById and
    /// QuotationTaskOwnershipService).
    ///
    /// Company matching follows the same task-driven rule as <see cref="BuildSqlClause"/>:
    ///   - Row has AssigneeCompanyId → caller's CompanyId must match it.
    ///   - Row has no AssigneeCompanyId (team-only, e.g. "ExtAdmin:Team_&lt;guid&gt;") → group/team
    ///     membership alone is sufficient; no company gate.
    /// </summary>
    public static bool IsOwner(
        string? assigneeUserId,
        Guid? assigneeCompanyId,
        IReadOnlyList<string> userGroups,
        string? userTeamId,
        Guid? callerCompanyId,
        string? username = null)
    {
        if (assigneeUserId is null)
            return false;

        var candidates = BuildCandidateSet(userGroups, userTeamId, username);
        if (!candidates.Contains(assigneeUserId))
            return false;

        // Task is scoped to a specific company — caller must belong to that company.
        if (assigneeCompanyId.HasValue)
            return callerCompanyId == assigneeCompanyId;

        // Task is team-only (no company scope) — group/team membership above is enough.
        return true;
    }

    private static HashSet<string> BuildCandidateSet(
        IReadOnlyList<string> userGroups,
        string? userTeamId,
        string? username)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);

        // Include the username directly so direct-assignment tasks (AssignedType='1') match.
        if (username is not null)
            set.Add(username);

        foreach (var g in userGroups)
        {
            set.Add(g);
            if (userTeamId is not null)
                set.Add($"{g}:Team_{userTeamId}");
        }
        return set;
    }
}

public sealed record PoolTaskAccessClause(string Sql, IReadOnlyDictionary<string, object?> Parameters);
