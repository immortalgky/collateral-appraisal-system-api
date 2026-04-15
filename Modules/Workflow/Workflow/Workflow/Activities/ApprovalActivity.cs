using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Workflow.Data.Repository;
using Workflow.Domain;
using Workflow.Domain.Committees;
using Workflow.Workflow.Activities.Approval;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Engine.Expression;
using Workflow.Workflow.Events;
using Workflow.Workflow.Models;
using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Activities;

public class ApprovalActivity : WorkflowActivityBase
{
    private readonly IApprovalMemberResolver _memberResolver;
    private readonly IApprovalVoteRepository _voteRepository;
    private readonly IPublisher _publisher;
    private readonly ExpressionEvaluator _expressionEvaluator;
    private readonly ILogger<ApprovalActivity> _logger;

    public ApprovalActivity(
        IApprovalMemberResolver memberResolver,
        IApprovalVoteRepository voteRepository,
        IPublisher publisher,
        ILogger<ApprovalActivity> logger)
    {
        _memberResolver = memberResolver;
        _voteRepository = voteRepository;
        _publisher = publisher;
        _expressionEvaluator = new ExpressionEvaluator();
        _logger = logger;
    }

    public override string ActivityType => ActivityTypes.ApprovalActivity;
    public override string Name => "Approval Activity";
    public override string Description => "Multi-member committee approval with quorum and majority voting";

    protected override async Task<ActivityResult> ExecuteActivityAsync(ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var memberSourceConfig = GetProperty<MemberSourceConfig>(context, "memberSource");
            if (memberSourceConfig is null)
                return ActivityResult.Failed("memberSource property is required for ApprovalActivity");

            var inlineQuorum = GetProperty<QuorumConfig>(context, "quorum");
            var inlineMajority = GetProperty<MajorityConfig>(context, "majority");

            // Resolve members, quorum, majority, conditions
            var groupInfo = await _memberResolver.ResolveMembersAsync(
                memberSourceConfig, context.Variables, inlineQuorum, inlineMajority, cancellationToken);

            var activityName = GetProperty(context, "activityName", context.ActivityId);
            var voteOptions = GetProperty<List<string>>(context, "voteOptions",
                new List<string> { "approve", "reject", "route_back" });

            // Build member assignments with unique task names
            var activityDisplayName = context.ActivityName;
            var memberAssignments = groupInfo.Members
                .Select(m => new ApprovalMemberAssignment(m.Username, $"{activityName}:{m.Username}", activityDisplayName))
                .ToList();

            // Store resolved config in output data for ResumeActivityAsync
            var outputData = new Dictionary<string, object>
            {
                [$"{NormalizeActivityId(context.ActivityId)}_members"] = groupInfo.Members,
                [$"{NormalizeActivityId(context.ActivityId)}_quorum"] = groupInfo.Quorum,
                [$"{NormalizeActivityId(context.ActivityId)}_majority"] = groupInfo.Majority,
                [$"{NormalizeActivityId(context.ActivityId)}_conditions"] = groupInfo.Conditions,
                [$"{NormalizeActivityId(context.ActivityId)}_voteOptions"] = voteOptions,
                [$"{NormalizeActivityId(context.ActivityId)}_committeeName"] = groupInfo.CommitteeName ?? "",
                [$"{NormalizeActivityId(context.ActivityId)}_committeeCode"] = groupInfo.CommitteeCode ?? "",
                [$"{NormalizeActivityId(context.ActivityId)}_totalMembers"] = groupInfo.Members.Count,
                [$"{NormalizeActivityId(context.ActivityId)}_votesReceived"] = 0,
                ["activityName"] = activityName
            };

            // Calculate SLA from timeout property
            DateTime? dueAt = null;
            var timeoutDuration = GetProperty<string>(context, "timeoutDuration");
            if (!string.IsNullOrEmpty(timeoutDuration))
            {
                try
                {
                    var timeout = System.Xml.XmlConvert.ToTimeSpan(timeoutDuration);
                    dueAt = DateTime.UtcNow.Add(timeout);
                }
                catch { /* ignore invalid format */ }
            }

            // Use CorrelationId if available
            var correlationGuid = !string.IsNullOrEmpty(context.WorkflowInstance.CorrelationId)
                && Guid.TryParse(context.WorkflowInstance.CorrelationId, out var parsed)
                    ? parsed
                    : context.WorkflowInstance.Id;

            var appraisalNumber = context.WorkflowInstance.Variables.TryGetValue("appraisalNumber", out var appraisalNumObj)
                ? appraisalNumObj?.ToString()
                : null;

            // Publish event to create PendingTasks for all members
            await _publisher.Publish(new ApprovalTasksAssignedEvent(
                correlationGuid,
                activityName,
                memberAssignments,
                DateTime.UtcNow,
                context.WorkflowInstanceId,
                context.ActivityId,
                dueAt,
                context.WorkflowInstance.StartedBy,
                context.WorkflowInstance.Name,
                appraisalNumber), cancellationToken);

            _logger.LogInformation(
                "ApprovalActivity {ActivityId} started with {MemberCount} members, committee={CommitteeCode}",
                context.ActivityId, groupInfo.Members.Count, groupInfo.CommitteeCode ?? "inline");

            return ActivityResult.Pending(outputData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing ApprovalActivity {ActivityId}", context.ActivityId);
            return ActivityResult.Failed($"ApprovalActivity failed: {ex.Message}");
        }
    }

    protected override async Task<ActivityResult> ResumeActivityAsync(ActivityContext context,
        Dictionary<string, object> resumeInput, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedId = NormalizeActivityId(context.ActivityId);

            // Get execution ID for this round
            var execution = FindActivityExecution(context);
            if (execution is null)
                return ActivityResult.Failed("No in-progress execution found");

            var executionId = execution.Id;

            // Extract voter info from resume input
            var voter = resumeInput.TryGetValue("completedBy", out var cb) ? cb?.ToString() : null;
            if (string.IsNullOrEmpty(voter))
                return ActivityResult.Failed("completedBy is required");

            var vote = resumeInput.TryGetValue("decisionTaken", out var dt) ? dt?.ToString() : null;
            if (string.IsNullOrEmpty(vote))
                return ActivityResult.Failed("decisionTaken is required");

            var comments = resumeInput.TryGetValue("comments", out var c) ? c?.ToString() : null;

            // Get stored config from workflow variables
            var members = GetVariable<List<ApprovalMemberInfo>>(context, $"{normalizedId}_members",
                new List<ApprovalMemberInfo>());
            var quorumConfig = GetVariable<QuorumConfig>(context, $"{normalizedId}_quorum",
                new QuorumConfig("Fixed", 1));
            var majorityConfig = GetVariable<MajorityConfig>(context, $"{normalizedId}_majority",
                new MajorityConfig("Simple", "approve"));
            var conditions = GetVariable<List<ApprovalConditionInfo>>(context, $"{normalizedId}_conditions",
                new List<ApprovalConditionInfo>());
            var voteOptions = GetVariable<List<string>>(context, $"{normalizedId}_voteOptions",
                new List<string> { "approve", "reject", "route_back" });
            var activityName = GetVariable(context, "activityName", context.ActivityId);

            // Validate vote option
            if (!voteOptions.Contains(vote, StringComparer.OrdinalIgnoreCase))
                return ActivityResult.Failed($"Invalid vote option '{vote}'. Allowed: {string.Join(", ", voteOptions)}");

            // Validate voter is a member
            var memberInfo = members.FirstOrDefault(m =>
                string.Equals(m.Username, voter, StringComparison.OrdinalIgnoreCase));
            if (memberInfo is null)
                return ActivityResult.Failed($"User '{voter}' is not a member of this approval group");

            // Get CorrelationId
            var correlationGuid = !string.IsNullOrEmpty(context.WorkflowInstance.CorrelationId)
                && Guid.TryParse(context.WorkflowInstance.CorrelationId, out var parsed)
                    ? parsed
                    : context.WorkflowInstance.Id;

            // Immediate route-back check
            if (vote.Equals("route_back", StringComparison.OrdinalIgnoreCase))
            {
                // Save the route_back vote for audit
                var rbVote = ApprovalVote.Create(
                    context.WorkflowInstanceId, context.ActivityId, executionId,
                    voter, memberInfo.Role, vote, comments);
                await _voteRepository.AddVoteAsync(rbVote, cancellationToken);

                // Publish task completed for this member
                await _publisher.Publish(new TaskCompletedDomainEvent(
                    correlationGuid, $"{activityName}:{voter}", vote, DateTime.UtcNow, voter,
                    context.WorkflowInstance.Name), cancellationToken);

                var rbOutput = BuildOutputData(normalizedId, "route_back", 0, 0, 1,
                    1, members.Count,
                    GetVariable<string>(context, $"{normalizedId}_committeeName"),
                    GetVariable<string>(context, $"{normalizedId}_committeeCode"));
                rbOutput["decision"] = "route_back";

                _logger.LogInformation(
                    "ApprovalActivity {ActivityId}: {Voter} voted route_back - immediate return",
                    context.ActivityId, voter);

                return ActivityResult.Success(rbOutput);
            }

            // Check duplicate vote
            var hasVoted = await _voteRepository.HasMemberVotedAsync(executionId, voter, cancellationToken);
            if (hasVoted)
                return ActivityResult.Failed($"Member '{voter}' has already voted in this round");

            // Record the vote. The DB has a unique index on (ActivityExecutionId, Member)
            // so a concurrent duplicate will throw DbUpdateException even if HasMemberVotedAsync passed.
            ApprovalVote approvalVote;
            try
            {
                approvalVote = ApprovalVote.Create(
                    context.WorkflowInstanceId, context.ActivityId, executionId,
                    voter, memberInfo.Role, vote, comments);
                await _voteRepository.AddVoteAsync(approvalVote, cancellationToken);
            }
            catch (DbUpdateException)
            {
                return ActivityResult.Failed("Member has already voted in this round");
            }

            // Publish task completed for this member
            await _publisher.Publish(new TaskCompletedDomainEvent(
                correlationGuid, $"{activityName}:{voter}", vote, DateTime.UtcNow, voter,
                context.WorkflowInstance.Name), cancellationToken);

            // Get all votes for this round
            var allVotes = await _voteRepository.GetVotesForExecutionAsync(executionId, cancellationToken);

            var targetVote = majorityConfig.TargetVote ?? "approve";
            var approveCount = allVotes.Count(v => v.Vote.Equals(targetVote, StringComparison.OrdinalIgnoreCase));
            var rejectCount = allVotes.Count(v => v.Vote.Equals("reject", StringComparison.OrdinalIgnoreCase));
            var routeBackCount = allVotes.Count(v => v.Vote.Equals("route_back", StringComparison.OrdinalIgnoreCase));
            var totalVotes = allVotes.Count;
            var totalMembers = members.Count;

            _logger.LogInformation(
                "ApprovalActivity {ActivityId}: Vote recorded by {Voter}={Vote}. Total: {TotalVotes}/{TotalMembers}, Approve={Approve}, Reject={Reject}",
                context.ActivityId, voter, vote, totalVotes, totalMembers, approveCount, rejectCount);

            // Check approval conditions
            var conditionsMet = CheckApprovalConditions(conditions, allVotes, targetVote);

            // Check quorum
            var requiredQuorum = GetRequiredQuorum(quorumConfig, totalMembers);
            var quorumMet = totalVotes >= requiredQuorum;

            // Check majority
            var majorityMet = CheckMajority(majorityConfig, approveCount, totalVotes, totalMembers);

            // Concurrent quorum completion safety: if two voters both reach this point
            // simultaneously and both trigger Success, the WorkflowEngine's FindActivityExecution
            // only returns executions with Status == InProgress. The first voter's Success will
            // mark the execution as Completed, so the second voter's request will find no
            // in-progress execution and return Failed("No in-progress execution found").
            if (conditionsMet && quorumMet && majorityMet)
            {
                var decision = approveCount > rejectCount ? targetVote : "reject";
                var successOutput = BuildOutputData(normalizedId, decision, approveCount, rejectCount,
                    routeBackCount, totalVotes, totalMembers,
                    GetVariable<string>(context, $"{normalizedId}_committeeName"),
                    GetVariable<string>(context, $"{normalizedId}_committeeCode"));
                successOutput["decision"] = decision;

                // Evaluate decision conditions if configured
                var decisionConditions = GetProperty<Dictionary<string, string>>(context, "decisionConditions");
                if (decisionConditions != null && decisionConditions.Any())
                {
                    var combinedVars = new Dictionary<string, object>(context.Variables);
                    foreach (var kvp in successOutput) combinedVars[kvp.Key] = kvp.Value;
                    combinedVars[$"{normalizedId}_decision"] = decision;

                    foreach (var dc in decisionConditions)
                    {
                        try
                        {
                            if (_expressionEvaluator.EvaluateExpression(dc.Value, combinedVars))
                            {
                                successOutput["decision"] = dc.Key;
                                break;
                            }
                        }
                        catch { /* skip failed conditions */ }
                    }
                }

                _logger.LogInformation(
                    "ApprovalActivity {ActivityId}: Decision reached - {Decision}. Votes: {Approve}/{Reject}/{RouteBack}",
                    context.ActivityId, successOutput["decision"], approveCount, rejectCount, routeBackCount);

                return ActivityResult.Success(successOutput);
            }

            // Not yet decided — return Pending with current state
            var pendingOutput = BuildOutputData(normalizedId, null, approveCount, rejectCount,
                routeBackCount, totalVotes, totalMembers,
                GetVariable<string>(context, $"{normalizedId}_committeeName"),
                GetVariable<string>(context, $"{normalizedId}_committeeCode"));
            pendingOutput[$"{normalizedId}_votesReceived"] = totalVotes;
            pendingOutput[$"{normalizedId}_quorumMet"] = quorumMet;
            pendingOutput[$"{normalizedId}_majorityMet"] = majorityMet;

            return ActivityResult.Pending(pendingOutput);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming ApprovalActivity {ActivityId}", context.ActivityId);
            return ActivityResult.Failed($"ApprovalActivity resume failed: {ex.Message}");
        }
    }

    public override Task<Core.ValidationResult> ValidateAsync(ActivityContext context,
        CancellationToken cancellationToken = default)
    {
        var memberSource = GetProperty<MemberSourceConfig>(context, "memberSource");
        if (memberSource is null)
            return Task.FromResult(Core.ValidationResult.Failure("memberSource is required for ApprovalActivity"));

        return Task.FromResult(Core.ValidationResult.Success());
    }

    protected override WorkflowActivityExecution CreateActivityExecution(ActivityContext context)
    {
        return WorkflowActivityExecution.Create(
            context.WorkflowInstance.Id,
            context.ActivityId,
            Name,
            ActivityType,
            null,
            context.Variables);
    }

    // -- Private helpers --

    private static bool CheckApprovalConditions(
        List<ApprovalConditionInfo> conditions, List<ApprovalVote> votes, string targetVote)
    {
        if (conditions.Count == 0) return true;

        foreach (var condition in conditions)
        {
            switch (condition.ConditionType)
            {
                case "RoleRequired":
                    if (!string.IsNullOrEmpty(condition.RoleRequired))
                    {
                        var roleVoted = votes.Any(v =>
                            string.Equals(v.MemberRole, condition.RoleRequired, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(v.Vote, targetVote, StringComparison.OrdinalIgnoreCase));
                        if (!roleVoted) return false;
                    }
                    break;

                case "MinVotes":
                    if (condition.MinVotesRequired.HasValue)
                    {
                        var targetCount = votes.Count(v =>
                            string.Equals(v.Vote, targetVote, StringComparison.OrdinalIgnoreCase));
                        if (targetCount < condition.MinVotesRequired.Value) return false;
                    }
                    break;
            }
        }

        return true;
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

    private static Dictionary<string, object> BuildOutputData(
        string normalizedId, string? decision, int approveCount, int rejectCount,
        int routeBackCount, int totalVotes, int totalMembers,
        string? committeeName, string? committeeCode)
    {
        var output = new Dictionary<string, object>
        {
            [$"{normalizedId}_votesApprove"] = approveCount,
            [$"{normalizedId}_votesReject"] = rejectCount,
            [$"{normalizedId}_votesRouteBack"] = routeBackCount,
            [$"{normalizedId}_totalVotes"] = totalVotes,
            [$"{normalizedId}_totalMembers"] = totalMembers,
            [$"{normalizedId}_committeeName"] = committeeName ?? "",
            [$"{normalizedId}_committeeCode"] = committeeCode ?? ""
        };

        if (decision is not null)
        {
            output[$"{normalizedId}_decision"] = decision;
        }

        return output;
    }
}
