using Shared.Identity;
using Workflow.Data.Repository;
using Workflow.Domain;
using Workflow.Domain.Committees;
using Workflow.Meetings.Domain;
using Workflow.Workflow.Activities.Approval;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Features.GetApprovalList;

public class GetApprovalListEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workflows/instances/{workflowInstanceId:guid}/activities/{activityId}/approval-list",
                async (
                    Guid workflowInstanceId,
                    string activityId,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var query = new GetApprovalListQuery(workflowInstanceId, activityId);
                    var result = await sender.Send(query, ct);
                    return Results.Ok(result);
                })
            .WithName("GetWorkflowApprovalList")
            .WithTags("Workflows")
            .RequireAuthorization()
            .Produces<GetApprovalListResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public record GetApprovalListQuery(Guid WorkflowInstanceId, string ActivityId)
    : IQuery<GetApprovalListResponse>;

public record GetApprovalListResponse(
    string ActivityId,
    string? CommitteeName,
    string? CommitteeCode,
    int? Tier,
    int TotalMembers,
    int VotesReceived,
    bool QuorumMet,
    bool MajorityMet,
    List<ApprovalMemberStatus> Members,
    List<ApprovalConditionStatus> Conditions,
    MeetingReference? MeetingRef);

public record ApprovalMemberStatus(
    string Username,
    string? Role,
    string Status,
    string? Vote,
    string? Comments,
    DateTime? VotedAt,
    bool IsCurrentUser);

public record ApprovalConditionStatus(
    string ConditionType,
    string? RoleRequired,
    int? MinVotesRequired,
    bool Met);

public record MeetingReference(
    Guid MeetingId,
    string Title,
    DateTime? StartAt,
    DateTime? EndedAt);

public class GetApprovalListQueryHandler(
    IWorkflowInstanceRepository workflowInstanceRepository,
    IApprovalVoteRepository voteRepository,
    WorkflowDbContext dbContext,
    ICurrentUserService currentUser
) : IQueryHandler<GetApprovalListQuery, GetApprovalListResponse>
{
    public async Task<GetApprovalListResponse> Handle(GetApprovalListQuery query, CancellationToken ct)
    {
        var instance = await workflowInstanceRepository.GetByIdAsync(query.WorkflowInstanceId, ct)
            ?? throw new NotFoundException($"Workflow instance {query.WorkflowInstanceId} not found");

        var normalizedId = query.ActivityId.Replace("-", "_");

        // Get stored member list from workflow variables
        var members = GetVariableAs<List<ApprovalMemberInfo>>(instance.Variables, $"{normalizedId}_members")
            ?? new List<ApprovalMemberInfo>();
        var committeeName = GetVariableAs<string>(instance.Variables, $"{normalizedId}_committeeName");
        var committeeCode = GetVariableAs<string>(instance.Variables, $"{normalizedId}_committeeCode");
        var conditions = GetVariableAs<List<ApprovalConditionInfo>>(instance.Variables, $"{normalizedId}_conditions")
            ?? new List<ApprovalConditionInfo>();

        // Find the current execution for this activity
        var execution = instance.ActivityExecutions
            .Where(e => e.ActivityId == query.ActivityId && e.Status == ActivityExecutionStatus.InProgress)
            .FirstOrDefault();

        List<ApprovalVote> votes = new();
        if (execution is not null)
            votes = await voteRepository.GetVotesForExecutionAsync(execution.Id, ct);

        var currentUsername = currentUser.Username;
        var memberStatuses = members.Select(m =>
        {
            var memberVote = votes.FirstOrDefault(v =>
                string.Equals(v.Member, m.Username, StringComparison.OrdinalIgnoreCase));

            return new ApprovalMemberStatus(
                m.Username,
                m.Role,
                memberVote is not null ? "Voted" : "Pending",
                memberVote?.Vote,
                memberVote?.Comments,
                memberVote?.VotedAt,
                !string.IsNullOrEmpty(currentUsername)
                    && string.Equals(currentUsername, m.Username, StringComparison.OrdinalIgnoreCase));
        }).ToList();

        var totalVotes = votes.Count;
        var totalMembers = members.Count;

        // Simple quorum/majority check for display
        var quorumConfig = GetVariableAs<QuorumConfig>(instance.Variables, $"{normalizedId}_quorum");
        var majorityConfig = GetVariableAs<MajorityConfig>(instance.Variables, $"{normalizedId}_majority");

        var requiredQuorum = quorumConfig is not null
            ? GetRequiredQuorum(quorumConfig, totalMembers) : totalMembers;
        var quorumMet = totalVotes >= requiredQuorum;

        var targetVote = majorityConfig?.TargetVote ?? "approve";
        var targetCount = votes.Count(v =>
            string.Equals(v.Vote, targetVote, StringComparison.OrdinalIgnoreCase));
        var majorityMet = majorityConfig is not null
            ? CheckMajority(majorityConfig, targetCount, totalVotes, totalMembers) : false;

        // Evaluate conditions against actual votes (per-condition met flag for UI)
        var conditionStatuses = conditions.Select(c => new ApprovalConditionStatus(
            c.ConditionType,
            c.RoleRequired,
            c.MinVotesRequired,
            EvaluateCondition(c, members, votes))).ToList();

        // Derive tier from committee code (1/2/3 for SUB_COMMITTEE/COMMITTEE/COMMITTEE_WITH_MEETING)
        var tier = DeriveTier(committeeCode);

        // Look up meeting reference: find a Meeting that contains this WorkflowInstance
        // (most recent item). COMMITTEE_WITH_MEETING approvals run after the meeting ended.
        MeetingReference? meetingRef = null;
        var meeting = await (from mi in dbContext.MeetingItems
                             join m in dbContext.Meetings on mi.MeetingId equals m.Id
                             where mi.WorkflowInstanceId == query.WorkflowInstanceId
                             orderby mi.AddedAt descending
                             select new { m.Id, m.Title, m.StartAt, m.EndedAt, m.Status })
                           .FirstOrDefaultAsync(ct);
        if (meeting is not null)
        {
            meetingRef = new MeetingReference(meeting.Id, meeting.Title, meeting.StartAt, meeting.EndedAt);
        }

        return new GetApprovalListResponse(
            query.ActivityId,
            committeeName,
            committeeCode,
            tier,
            totalMembers,
            totalVotes,
            quorumMet,
            majorityMet,
            memberStatuses,
            conditionStatuses,
            meetingRef);
    }

    private static int? DeriveTier(string? committeeCode) => committeeCode switch
    {
        "SUB_COMMITTEE" => 1,
        "COMMITTEE" => 2,
        "COMMITTEE_WITH_MEETING" => 3,
        _ => null
    };

    private static bool EvaluateCondition(
        ApprovalConditionInfo condition,
        List<ApprovalMemberInfo> members,
        List<ApprovalVote> votes)
    {
        switch (condition.ConditionType)
        {
            case nameof(ConditionType.RoleRequired):
                if (string.IsNullOrEmpty(condition.RoleRequired)) return true;
                // Requires a member holding the given role to have cast an "approve" vote.
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

    private static T? GetVariableAs<T>(Dictionary<string, object> variables, string key)
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

    private static int GetRequiredQuorum(QuorumConfig config, int totalMembers)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "fixed" => config.Value,
            "percentage" => (int)Math.Ceiling(totalMembers * config.Value / 100.0),
            _ => totalMembers
        };
    }

    private static bool CheckMajority(MajorityConfig config, int targetCount, int totalVotes, int totalMembers)
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
