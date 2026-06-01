using Microsoft.EntityFrameworkCore;
using Shared.Identity;
using Workflow.Data.Repository;
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
    ICurrentUserService currentUser
) : IQueryHandler<GetApprovalHistoryQuery, GetApprovalListResponse?>
{
    public async Task<GetApprovalListResponse?> Handle(GetApprovalHistoryQuery query, CancellationToken ct)
    {
        var votes = await voteRepository.GetLatestRoundVotesByAppraisalAsync(
            query.AppraisalId, query.ActivityId, ct);

        if (votes.Count == 0) return null;

        // All votes in the latest round share the same WorkflowInstanceId.
        // The member roster / committee config all live in the instance variables; without
        // it we could only return a vote list we cannot map to members, so treat it as 404.
        var workflowInstanceId = votes[0].WorkflowInstanceId;
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
            meetingRef);
    }
}
