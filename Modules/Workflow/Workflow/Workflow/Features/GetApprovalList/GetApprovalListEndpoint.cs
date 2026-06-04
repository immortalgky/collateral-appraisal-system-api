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

    private static int? DeriveTier(string? committeeCode) =>
        ApprovalListProjection.DeriveTier(committeeCode);

    private static bool EvaluateCondition(
        ApprovalConditionInfo condition,
        List<ApprovalMemberInfo> members,
        List<ApprovalVote> votes) =>
        ApprovalListProjection.EvaluateCondition(condition, members, votes);

    private static T? GetVariableAs<T>(Dictionary<string, object> variables, string key) =>
        ApprovalListProjection.GetVariableAs<T>(variables, key);

    private static int GetRequiredQuorum(QuorumConfig config, int totalMembers) =>
        ApprovalListProjection.GetRequiredQuorum(config, totalMembers);

    private static bool CheckMajority(MajorityConfig config, int targetCount, int totalVotes, int totalMembers) =>
        ApprovalListProjection.CheckMajority(config, targetCount, totalVotes, totalMembers);
}
