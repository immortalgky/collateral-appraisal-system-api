using Workflow.Data.Repository;
using Workflow.Domain;
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
    int TotalMembers,
    int VotesReceived,
    bool QuorumMet,
    bool MajorityMet,
    List<ApprovalMemberStatus> Members);

public record ApprovalMemberStatus(
    string Username, string? Role, string Status,
    string? Vote, string? Comments, DateTime? VotedAt);

public class GetApprovalListQueryHandler(
    IWorkflowInstanceRepository workflowInstanceRepository,
    IApprovalVoteRepository voteRepository
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

        // Find the current execution for this activity
        var execution = instance.ActivityExecutions
            .Where(e => e.ActivityId == query.ActivityId && e.Status == ActivityExecutionStatus.InProgress)
            .FirstOrDefault();

        List<ApprovalVote> votes = new();
        if (execution is not null)
            votes = await voteRepository.GetVotesForExecutionAsync(execution.Id, ct);

        var memberStatuses = members.Select(m =>
        {
            var memberVote = votes.FirstOrDefault(v =>
                string.Equals(v.Member, m.Username, StringComparison.OrdinalIgnoreCase));

            return new ApprovalMemberStatus(
                m.Username, m.Role,
                memberVote is not null ? "Voted" : "Pending",
                memberVote?.Vote, memberVote?.Comments, memberVote?.VotedAt);
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

        return new GetApprovalListResponse(
            query.ActivityId, committeeName, totalMembers, totalVotes,
            quorumMet, majorityMet, memberStatuses);
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
