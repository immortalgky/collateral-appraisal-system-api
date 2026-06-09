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

    // Mirrors ApprovalActivity.CheckMajority — the config string is the round-tripped MajorityType
    // name. Delegates to the shared domain rule (all-members denominator); unknown type → false.
    internal static bool CheckMajority(MajorityConfig config, int targetCount, int totalVotes, int totalMembers)
    {
        return Enum.TryParse<MajorityType>(config.Type, ignoreCase: true, out var majorityType)
            && MajorityRule.IsMet(majorityType, targetCount, totalMembers);
    }

    /// <summary>
    /// Authoritative read-side status for the approval round, mirroring the decision rule in
    /// <c>ApprovalActivity.ResumeActivityAsync</c> so the UI never shows "Approved" before the round
    /// would actually resolve. WaitForAll requires every member to have voted; Quorum resolves once
    /// quorum is met. A single route_back means the round was sent back.
    /// </summary>
    internal static string DeriveStatus(
        string? votingMode,
        int totalVotes,
        int totalMembers,
        bool quorumMet,
        bool majorityMet,
        bool conditionsMet,
        List<ApprovalVote> votes)
    {
        if (votes.Any(v => string.Equals(v.Vote, "route_back", StringComparison.OrdinalIgnoreCase)))
            return "Returned";

        var requireAllVotes = string.Equals(votingMode, "WaitForAll", StringComparison.OrdinalIgnoreCase);
        var allVoted = totalVotes >= totalMembers;

        var approved = requireAllVotes
            ? allVoted && majorityMet && conditionsMet
            : quorumMet && majorityMet && conditionsMet;

        return approved ? "Approved" : "Pending";
    }
}
