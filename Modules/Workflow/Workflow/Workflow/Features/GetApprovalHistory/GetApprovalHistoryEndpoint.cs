using Dapper;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Identity;
using Workflow.Data.Repository;
using Workflow.Domain;
using Workflow.Workflow.Activities.Approval;
using Workflow.Workflow.Features.GetApprovalList;
using Workflow.Workflow.Repositories;

namespace Workflow.Workflow.Features.GetApprovalHistory;

public class GetApprovalHistoryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/appraisals/{appraisalId:guid}/approval-history",
                async (
                    Guid appraisalId,
                    string? activityId,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var query = new GetApprovalHistoryQuery(
                        appraisalId,
                        string.IsNullOrWhiteSpace(activityId) ? "pending-approval" : activityId);
                    var result = await sender.Send(query, ct);
                    return result is null
                        ? Results.NotFound()
                        : Results.Ok(result);
                })
            .WithName("GetAppraisalApprovalHistory")
            .WithTags("Appraisals", "Workflows")
            .RequireAuthorization()
            .Produces<GetApprovalListResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public record GetApprovalHistoryQuery(Guid AppraisalId, string ActivityId = "pending-approval")
    : IQuery<GetApprovalListResponse?>;

public class GetApprovalHistoryQueryHandler(
    IApprovalVoteRepository voteRepository,
    IWorkflowInstanceRepository workflowInstanceRepository,
    WorkflowDbContext dbContext,
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser
) : IQueryHandler<GetApprovalHistoryQuery, GetApprovalListResponse?>
{
    public async Task<GetApprovalListResponse?> Handle(GetApprovalHistoryQuery query, CancellationToken ct)
    {
        var votes = await voteRepository.GetLatestRoundVotesByAppraisalAsync(
            query.AppraisalId, query.ActivityId, ct);

        if (votes.Count == 0) return null;

        // Legacy-imported rounds have no WorkflowInstance — their committee config never lived in
        // instance variables. Serve those from the persisted appraisal-side outcome instead.
        if (votes[0].WorkflowInstanceId is null)
            return await BuildFromPersistedOutcomeAsync(query, votes, ct);

        // All votes in the latest round share the same WorkflowInstanceId.
        // The member roster / committee config all live in the instance variables; without
        // it we could only return a vote list we cannot map to members, so treat it as 404.
        var workflowInstanceId = votes[0].WorkflowInstanceId!.Value;
        var instance = await workflowInstanceRepository.GetByIdAsync(workflowInstanceId, ct);
        if (instance is null) return null;

        var normalizedId = query.ActivityId.Replace("-", "_");

        var members = ApprovalListProjection.GetVariableAs<List<ApprovalMemberInfo>>(instance.Variables, $"{normalizedId}_members")
            ?? new List<ApprovalMemberInfo>();
        var committeeName = ApprovalListProjection.GetVariableAs<string>(instance.Variables, $"{normalizedId}_committeeName");
        var committeeCode = ApprovalListProjection.GetVariableAs<string>(instance.Variables, $"{normalizedId}_committeeCode");
        var conditions = ApprovalListProjection.GetVariableAs<List<ApprovalConditionInfo>>(instance.Variables, $"{normalizedId}_conditions")
            ?? new List<ApprovalConditionInfo>();
        var quorumConfig = ApprovalListProjection.GetVariableAs<QuorumConfig>(instance.Variables, $"{normalizedId}_quorum");
        var majorityConfig = ApprovalListProjection.GetVariableAs<MajorityConfig>(instance.Variables, $"{normalizedId}_majority");

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

        var requiredQuorum = quorumConfig is not null
            ? ApprovalListProjection.GetRequiredQuorum(quorumConfig, totalMembers) : totalMembers;
        var quorumMet = totalVotes >= requiredQuorum;

        var targetVote = majorityConfig?.TargetVote ?? "approve";
        var targetCount = votes.Count(v =>
            string.Equals(v.Vote, targetVote, StringComparison.OrdinalIgnoreCase));
        var majorityMet = majorityConfig is not null
            && ApprovalListProjection.CheckMajority(majorityConfig, targetCount, totalVotes, totalMembers);

        var conditionStatuses = conditions.Select(c => new ApprovalConditionStatus(
            c.ConditionType,
            c.RoleRequired,
            c.MinVotesRequired,
            ApprovalListProjection.EvaluateCondition(c, members, votes))).ToList();

        var votingMode = ApprovalListProjection.GetVariableAs<string>(instance.Variables, $"{normalizedId}_votingMode");
        var conditionsMet = conditionStatuses.All(c => c.Met);
        var status = ApprovalListProjection.DeriveStatus(
            votingMode, totalVotes, totalMembers, quorumMet, majorityMet, conditionsMet, votes);

        var tier = ApprovalListProjection.DeriveTier(committeeCode);

        // Look up meeting reference — same join as the active-list endpoint.
        MeetingReference? meetingRef = null;
        var meeting = await (from mi in dbContext.MeetingItems
                             join m in dbContext.Meetings on mi.MeetingId equals m.Id
                             where mi.WorkflowInstanceId == workflowInstanceId
                             orderby mi.AddedAt descending
                             select new { m.Id, m.Title, m.StartAt, m.EndedAt })
                           .FirstOrDefaultAsync(ct);
        if (meeting is not null)
            meetingRef = new MeetingReference(meeting.Id, meeting.Title, meeting.StartAt, meeting.EndedAt);

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
            meetingRef,
            status);
    }

    // Migrated / legacy path: no WorkflowInstance. Roster is rebuilt from the imported vote
    // rows themselves; committee identity + meeting come from the persisted outcome view.
    private async Task<GetApprovalListResponse?> BuildFromPersistedOutcomeAsync(
        GetApprovalHistoryQuery query, List<ApprovalVote> votes, CancellationToken ct)
    {
        var currentUsername = currentUser.Username;
        var memberStatuses = votes
            .OrderBy(v => v.VotedAt)
            .Select(v => new ApprovalMemberStatus(
                v.Member,
                v.MemberRole,
                "Voted",
                v.Vote,
                v.Comments,
                v.VotedAt,
                !string.IsNullOrEmpty(currentUsername)
                    && string.Equals(currentUsername, v.Member, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var connection = connectionFactory.GetOpenConnection();
        var outcome = await connection.QuerySingleOrDefaultAsync<ApprovalOutcomeRow>(
            new CommandDefinition(OutcomeSql, new { query.AppraisalId }, cancellationToken: ct));

        var totalVotes = votes.Count;

        // Authoritative outcome: Appraisals.ApprovedByCommittee is set (to the committee code) ONLY
        // when the committee approved — it already encodes whatever majority/quorum/role rule applied
        // at decision time, so we trust it rather than re-deriving from raw vote counts (which would
        // mislabel a non-simple-majority approval as "Returned"). The vote rows drive the display
        // roster only. `outcome` is null only for orphaned votes with no Appraisals row → not approved.
        var approved = !string.IsNullOrEmpty(outcome?.ApprovedByCommittee);

        // Committee code: prefer the review's committee; fall back to ApprovedByCommittee, which IS the
        // committee code (covers an approved appraisal whose AppraisalReviews row is absent).
        var committeeCode = !string.IsNullOrEmpty(outcome?.CommitteeCode)
            ? outcome!.CommitteeCode
            : outcome?.ApprovedByCommittee;

        MeetingReference? meetingRef = null;
        if (outcome?.MeetingId is { } meetingId && meetingId != Guid.Empty)
            meetingRef = new MeetingReference(
                meetingId, outcome.MeetingTitle ?? string.Empty, outcome.MeetingStartAt, outcome.MeetingEndedAt);

        return new GetApprovalListResponse(
            query.ActivityId,
            outcome?.CommitteeName,
            committeeCode,
            ApprovalListProjection.DeriveTier(committeeCode),
            totalVotes,                 // historical: every roster member is a recorded voter
            totalVotes,
            true,                       // a closed historical round had the quorum to decide
            approved,                   // majority reflects the recorded outcome
            memberStatuses,
            new List<ApprovalConditionStatus>(),
            meetingRef,
            approved ? "Approved" : "Returned");
    }

    private const string OutcomeSql = """
        SELECT TOP 1
            o.CommitteeCode,
            o.CommitteeName,
            o.ApprovedByCommittee,
            o.MeetingId,
            o.MeetingTitle,
            o.MeetingStartAt,
            o.MeetingEndedAt
        FROM appraisal.vw_AppraisalApprovalOutcome o
        WHERE o.AppraisalId = @AppraisalId
        """;

    private sealed class ApprovalOutcomeRow
    {
        public string? CommitteeCode { get; set; }
        public string? CommitteeName { get; set; }
        public string? ApprovedByCommittee { get; set; }
        public Guid? MeetingId { get; set; }
        public string? MeetingTitle { get; set; }
        public DateTime? MeetingStartAt { get; set; }
        public DateTime? MeetingEndedAt { get; set; }
    }
}
