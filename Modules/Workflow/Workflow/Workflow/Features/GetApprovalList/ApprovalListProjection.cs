using Workflow.Domain;
using Workflow.Domain.Committees;
using Workflow.Workflow.Activities.Approval;

namespace Workflow.Workflow.Features.GetApprovalList;

/// <summary>
/// Shared projection helpers used by both GetApprovalListEndpoint (live workflow)
/// and GetApprovalHistoryEndpoint (completed workflow). No behavior change to either.
/// </summary>
internal static class ApprovalListProjection
{
    internal static int? DeriveTier(string? committeeCode) => committeeCode switch
    {
        "SUB_COMMITTEE" => 1,
        "COMMITTEE" => 2,
        "COMMITTEE_WITH_MEETING" => 3,
        _ => null
    };

    internal static bool EvaluateCondition(
        ApprovalConditionInfo condition,
        List<ApprovalMemberInfo> members,
        List<ApprovalVote> votes)
    {
        switch (condition.ConditionType)
        {
            case nameof(ConditionType.RoleRequired):
                if (string.IsNullOrEmpty(condition.RoleRequired)) return true;
                var memberWithRole = members
                    .Where(m => string.Equals(m.Role, condition.RoleRequired, StringComparison.OrdinalIgnoreCase))
                    .Select(m => m.Username)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                return votes.Any(v =>
                    memberWithRole.Contains(v.Member) &&
                    string.Equals(v.Vote, "approve", StringComparison.OrdinalIgnoreCase));

            case nameof(ConditionType.MinVotes):
                var required = condition.MinVotesRequired ?? 0;
                return votes.Count(v => string.Equals(v.Vote, "approve", StringComparison.OrdinalIgnoreCase)) >= required;

            default:
                return false;
        }
    }

    internal static T? GetVariableAs<T>(Dictionary<string, object> variables, string key)
    {
        if (!variables.TryGetValue(key, out var value) || value is null)
            return default;

        if (value is T typed) return typed;

        if (value is System.Text.Json.JsonElement je)
        {
            try
            {
                return je.Deserialize<T>(new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
            }
            catch { return default; }
        }

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch { return default; }
    }

    internal static int GetRequiredQuorum(QuorumConfig config, int totalMembers)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "fixed" => config.Value,
            "percentage" => (int)Math.Ceiling(totalMembers * config.Value / 100.0),
            _ => totalMembers
        };
    }

    internal static bool CheckMajority(MajorityConfig config, int targetCount, int totalVotes, int totalMembers)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "simple" => targetCount > totalVotes / 2.0,
            "twothirds" => targetCount >= Math.Ceiling(totalVotes * 2.0 / 3.0),
            "unanimous" => targetCount == totalMembers,
            _ => false
        };
    }
}
