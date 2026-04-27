namespace Workflow.Tasks.Authorization;

public static class PoolTaskAccess
{
    /// <summary>Builds the SQL fragment + parameters that gate pool-task visibility for the caller.
    /// Returns null when the caller has no groups — caller should short-circuit to an empty result.</summary>
    public static PoolTaskAccessClause? BuildSqlClause(
        IReadOnlyList<string> userGroups,
        string? userTeamId,
        Guid? callerCompanyId)
    {
        if (userGroups.Count == 0)
            return null;

        var candidates = BuildCandidateSet(userGroups, userTeamId);

        var inParams = new Dictionary<string, object?>(StringComparer.Ordinal);
        var paramNames = new List<string>(candidates.Count);
        var i = 0;
        foreach (var c in candidates)
        {
            var key = $"PoolAssignee{i++}";
            paramNames.Add($"@{key}");
            inParams[key] = c;
        }

        inParams["PoolCallerCompanyId"] = callerCompanyId;

        var sql = $"(AssigneeUserId IN ({string.Join(", ", paramNames)}) AND (AssigneeCompanyId IS NULL OR AssigneeCompanyId = @PoolCallerCompanyId))";

        return new PoolTaskAccessClause(sql, inParams);
    }

    /// <summary>In-memory ownership check for a single hydrated row (used by GetTaskById).</summary>
    public static bool IsOwner(
        string? assigneeUserId,
        Guid? assigneeCompanyId,
        IReadOnlyList<string> userGroups,
        string? userTeamId,
        Guid? callerCompanyId)
    {
        if (assigneeUserId is null || userGroups.Count == 0)
            return false;

        var candidates = BuildCandidateSet(userGroups, userTeamId);
        return candidates.Contains(assigneeUserId)
               && (assigneeCompanyId is null || assigneeCompanyId == callerCompanyId);
    }

    private static HashSet<string> BuildCandidateSet(IReadOnlyList<string> userGroups, string? userTeamId)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
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
