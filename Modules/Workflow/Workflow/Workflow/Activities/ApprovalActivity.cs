using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
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
    private readonly IIntegrationEventOutbox _outbox;
    private readonly ICommitteeRepository _committeeRepository;
    private readonly ExpressionEvaluator _expressionEvaluator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ApprovalActivity> _logger;

    public ApprovalActivity(
        IApprovalMemberResolver memberResolver,
        IApprovalVoteRepository voteRepository,
        IPublisher publisher,
        IIntegrationEventOutbox outbox,
        ICommitteeRepository committeeRepository,
        IDateTimeProvider dateTimeProvider,
        ILogger<ApprovalActivity> logger)
    {
        _memberResolver = memberResolver;
        _voteRepository = voteRepository;
        _publisher = publisher;
        _outbox = outbox;
        _committeeRepository = committeeRepository;
        _dateTimeProvider = dateTimeProvider;
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

            // Member override: if the pending-meeting step supplied a manual member list, use it
            // instead of the committee's configured members. Quorum/majority still come from groupInfo.
            var overrideMembers = GetVariable<List<MeetingMemberOverride>>(context, "meetingMemberOverrides", []);
            var resolvedMembers = overrideMembers.Count > 0
                ? overrideMembers.Select(m => new ApprovalMemberInfo(m.UserId, m.Role)).ToList()
                : groupInfo.Members;

            if (overrideMembers.Count > 0)
            {
                var requiredQuorum = GetRequiredQuorum(groupInfo.Quorum, overrideMembers.Count);
                if (overrideMembers.Count < requiredQuorum)
                    _logger.LogWarning(
                        "ApprovalActivity {ActivityId}: override member count ({OverrideCount}) is below required quorum ({Quorum}); approval may never reach quorum",
                        context.ActivityId, overrideMembers.Count, requiredQuorum);
            }

            var activityName = GetProperty(context, "activityName", context.ActivityId);
            var voteOptions = GetProperty<List<string>>(context, "voteOptions",
                new List<string> { "approve", "reject", "route_back" });

            // Build member assignments with unique task names
            var activityDisplayName = context.ActivityName;
            var memberAssignments = resolvedMembers
                .Select(m => new ApprovalMemberAssignment(m.Username, $"{activityName}:{m.Username}", activityDisplayName))
                .ToList();

            // Store resolved config in output data for ResumeActivityAsync
            var outputData = new Dictionary<string, object>
            {
                [$"{NormalizeActivityId(context.ActivityId)}_members"] = resolvedMembers,
                [$"{NormalizeActivityId(context.ActivityId)}_quorum"] = groupInfo.Quorum,
                [$"{NormalizeActivityId(context.ActivityId)}_majority"] = groupInfo.Majority,
                [$"{NormalizeActivityId(context.ActivityId)}_votingMode"] = groupInfo.VotingMode,
                [$"{NormalizeActivityId(context.ActivityId)}_conditions"] = groupInfo.Conditions,
                [$"{NormalizeActivityId(context.ActivityId)}_voteOptions"] = voteOptions,
                [$"{NormalizeActivityId(context.ActivityId)}_committeeName"] = groupInfo.CommitteeName ?? "",
                [$"{NormalizeActivityId(context.ActivityId)}_committeeCode"] = groupInfo.CommitteeCode ?? "",
                [$"{NormalizeActivityId(context.ActivityId)}_totalMembers"] = resolvedMembers.Count,
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
                    dueAt = _dateTimeProvider.ApplicationNow.Add(timeout);
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
                _dateTimeProvider.ApplicationNow,
                context.WorkflowInstanceId,
                context.ActivityId,
                dueAt,
                context.WorkflowInstance.StartedBy,
                context.WorkflowInstance.Name,
                appraisalNumber,
                context.Movement,
                groupInfo.CommitteeCode), cancellationToken);

            _logger.LogInformation(
                "ApprovalActivity {ActivityId} started with {MemberCount} members, committee={CommitteeCode}, memberOverride={IsOverride}",
                context.ActivityId, resolvedMembers.Count, groupInfo.CommitteeCode ?? "inline", overrideMembers.Count > 0);

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
            // Default "Quorum" preserves the pre-existing early-decide behavior for any in-flight
            // round started before this variable was written.
            var votingMode = GetVariable<string>(context, $"{normalizedId}_votingMode", "Quorum");
            var requireAllVotes = string.Equals(votingMode, "WaitForAll", StringComparison.OrdinalIgnoreCase);
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
                var rbAppraisalId = ResolveAppraisalId(context.Variables);
                if (rbAppraisalId is null)
                    _logger.LogWarning(
                        "ApprovalActivity {ActivityId}: appraisalId missing from variables; route_back vote by {Voter} stored with empty AppraisalId and will be invisible to history.",
                        context.ActivityId, voter);

                // Save the route_back vote for audit
                var rbVote = ApprovalVote.Create(
                    rbAppraisalId ?? Guid.Empty,
                    context.WorkflowInstanceId, context.ActivityId, executionId,
                    voter, memberInfo.Role, vote, comments);
                await _voteRepository.AddVoteAsync(rbVote, cancellationToken);

                // Resolve movement ("B" for route_back). Stamp both the completed task
                // and the activity execution row so the next activity inherits "B".
                var rbMovement = ResolveActionMovement(context, vote);
                execution.StampMovement(rbMovement);

                // Publish task completed for this member
                await _publisher.Publish(new TaskCompletedDomainEvent(
                    correlationGuid, $"{activityName}:{voter}", vote, _dateTimeProvider.ApplicationNow, voter,
                    context.WorkflowInstance.Name, null, null, rbMovement,
                    rbAppraisalId, context.ActivityId), cancellationToken);

                var rbOutput = BuildOutputData(normalizedId, "route_back", 0, 0, 1,
                    1, members.Count,
                    GetVariable<string>(context, $"{normalizedId}_committeeName"),
                    GetVariable<string>(context, $"{normalizedId}_committeeCode"));
                rbOutput["decision"] = "route_back";

                // The round resolves immediately on a single route_back; close out the still-open
                // tasks of members who never voted so they don't dangle.
                await _publisher.Publish(new ApprovalRoundClosedEvent(
                    context.WorkflowInstanceId, context.ActivityId, _dateTimeProvider.ApplicationNow,
                    "RouteBack"), cancellationToken);

                _logger.LogInformation(
                    "ApprovalActivity {ActivityId}: {Voter} voted route_back - immediate return",
                    context.ActivityId, voter);

                return ActivityResult.Success(rbOutput);
            }

            // Check duplicate vote
            var hasVoted = await _voteRepository.HasMemberVotedAsync(executionId, voter, cancellationToken);
            if (hasVoted)
                return ActivityResult.Failed($"Member '{voter}' has already voted in this round");

            var voteAppraisalId = ResolveAppraisalId(context.Variables);
            if (voteAppraisalId is null)
                _logger.LogWarning(
                    "ApprovalActivity {ActivityId}: appraisalId missing from variables; vote by {Voter} stored with empty AppraisalId and will be invisible to history.",
                    context.ActivityId, voter);

            // Record the vote. The DB has a unique index on (ActivityExecutionId, Member)
            // so a concurrent duplicate will throw DbUpdateException even if HasMemberVotedAsync passed.
            ApprovalVote approvalVote;
            try
            {
                approvalVote = ApprovalVote.Create(
                    voteAppraisalId ?? Guid.Empty,
                    context.WorkflowInstanceId, context.ActivityId, executionId,
                    voter, memberInfo.Role, vote, comments);
                await _voteRepository.AddVoteAsync(approvalVote, cancellationToken);
            }
            catch (DbUpdateException)
            {
                return ActivityResult.Failed("Member has already voted in this round");
            }

            // Resolve movement for this vote — used on the per-member completed task
            // stamp and, if the quorum is reached below, on the activity execution row.
            var voteMovement = ResolveActionMovement(context, vote);

            // Publish task completed for this member
            await _publisher.Publish(new TaskCompletedDomainEvent(
                correlationGuid, $"{activityName}:{voter}", vote, _dateTimeProvider.ApplicationNow, voter,
                context.WorkflowInstance.Name, null, null, voteMovement,
                voteAppraisalId, context.ActivityId), cancellationToken);

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

            // Check majority (evaluated against ALL members, not just the votes cast)
            var majorityMet = CheckMajority(majorityConfig, approveCount, totalVotes, totalMembers);

            // Has every member voted this round?
            var allVoted = totalVotes >= totalMembers;

            // Decide whether the round resolves (advances). Only the APPROVE outcome advances —
            // when the approve condition is not met the round does nothing and stays Pending.
            //   WaitForAll (consensus): wait until everyone has voted, then require the approve rule.
            //   Quorum: resolve as soon as quorum + majority are met (may be before everyone votes).
            var decided = requireAllVotes
                ? allVoted && conditionsMet && majorityMet
                : conditionsMet && quorumMet && majorityMet;

            // Concurrent completion: approval resumes on the same instance are serialized by an
            // exclusive application lock acquired in WorkflowService.ExecuteResumeWorkflowAsync
            // ($"wf-approval:{instanceId}"), so two voters cannot evaluate decided==true at once — the
            // second waits, then reloads fresh state and either sees the round already Completed
            // (FindActivityExecution returns null → graceful Failed) or records its own vote. The
            // CompletedTask PK reuse (from PendingTask.Id) and the idempotent Appraisal-side
            // integration event remain as defense-in-depth.
            if (decided)
            {
                var decision = targetVote;
                var successOutput = BuildOutputData(normalizedId, decision, approveCount, rejectCount,
                    routeBackCount, totalVotes, totalMembers,
                    GetVariable<string>(context, $"{normalizedId}_committeeName"),
                    GetVariable<string>(context, $"{normalizedId}_committeeCode"));
                successOutput["decision"] = decision;
                execution.StampMovement(voteMovement);

                // Quorum mode can resolve before everyone has voted — close out the still-open
                // tasks of members who never voted so they don't dangle. (No-op when all voted.)
                if (!allVoted)
                {
                    await _publisher.Publish(new ApprovalRoundClosedEvent(
                        context.WorkflowInstanceId, context.ActivityId,
                        _dateTimeProvider.ApplicationNow, "Closed"), cancellationToken);
                }

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

                // Emit committee approval event when decision == "approve".
                // Consumers:
                //   - Appraisal module: stamps CompletedAt + ApprovedByCommittee on the aggregate.
                //   - Workflow Meetings: enqueues AppraisalAcknowledgementQueueItem (filtered by committee code).
                if (successOutput["decision"] is string finalDecision &&
                    string.Equals(finalDecision, "approve", StringComparison.OrdinalIgnoreCase))
                {
                    var appraisalId = ResolveAppraisalId(context.Variables);
                    if (appraisalId is not null)
                    {
                        var committeeCode = GetVariable<string>(context, $"{normalizedId}_committeeCode") ?? string.Empty;
                        var committeeName = GetVariable<string>(context, $"{normalizedId}_committeeName");
                        var appraisalNo = context.WorkflowInstance.Variables.TryGetValue("appraisalNumber", out var appraisalNumObj)
                            ? appraisalNumObj?.ToString()
                            : null;

                        // Resolve CommitteeId for the ack-queue consumer. A missing committee row is
                        // non-fatal — we still publish the event (the Appraisal consumer still needs it);
                        // the ack consumer will just not find a mapping in AcknowledgementGroupSettings
                        // and will skip silently.
                        Guid committeeId = Guid.Empty;
                        if (!string.IsNullOrEmpty(committeeCode))
                        {
                            try
                            {
                                var committee = await _committeeRepository.GetByCodeAsync(committeeCode, cancellationToken);
                                if (committee is not null)
                                {
                                    committeeId = committee.Id;
                                }
                                else
                                {
                                    _logger.LogWarning(
                                        "ApprovalActivity {ActivityId}: Committee with code '{CommitteeCode}' not found; CommitteeId will be Guid.Empty in event",
                                        context.ActivityId, committeeCode);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex,
                                    "ApprovalActivity {ActivityId}: Failed to resolve CommitteeId for code '{CommitteeCode}'; CommitteeId will be Guid.Empty in event",
                                    context.ActivityId, committeeCode);
                            }
                        }

                        // For committee-with-meeting (tier 3) the preceding MeetingActivity stamped a
                        // "{normalized}_meetingId" variable. Scan for it; absent for direct approvals.
                        Guid? decisionMeetingId = null;
                        foreach (var kv in context.Variables)
                        {
                            if (kv.Key.EndsWith("_meetingId", StringComparison.OrdinalIgnoreCase)
                                && Guid.TryParse(kv.Value?.ToString(), out var parsedMeetingId)
                                && parsedMeetingId != Guid.Empty)
                            {
                                decisionMeetingId = parsedMeetingId;
                                break;
                            }
                        }

                        _outbox.Publish(new AppraisalApprovedIntegrationEvent
                        {
                            AppraisalId = appraisalId.Value,
                            CommitteeCode = committeeCode,
                            CommitteeName = committeeName,
                            ApprovedAt = _dateTimeProvider.ApplicationNow,
                            ApprovedBy = voter,
                            AppraisalNo = appraisalNo,
                            CommitteeId = committeeId,
                            VotesApprove = approveCount,
                            VotesReject = rejectCount,
                            VotesRouteBack = routeBackCount,
                            DecisionMeetingId = decisionMeetingId
                        }, appraisalId.Value.ToString());

                        _logger.LogInformation(
                            "ApprovalActivity {ActivityId}: Published AppraisalApprovedIntegrationEvent for AppraisalId {AppraisalId} CommitteeCode {CommitteeCode} CommitteeId {CommitteeId}",
                            context.ActivityId, appraisalId.Value, committeeCode, committeeId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "ApprovalActivity {ActivityId}: Decision=approve but no appraisalId in workflow variables; skipping committee approval emit",
                            context.ActivityId);
                    }
                }

                return ActivityResult.Success(successOutput);
            }

            // WaitForAll: everyone has voted but the approve rule was not met. By design the round
            // does nothing (stays Pending, application not completed) — but at this point every
            // per-member task has been removed, so the round is wedged with no in-band action left.
            // Log it so the stuck state is observable; recovery is an out-of-band workflow cancel.
            if (requireAllVotes && allVoted)
            {
                _logger.LogWarning(
                    "ApprovalActivity {ActivityId}: all {TotalMembers} members voted but approve rule not met " +
                    "(approve={Approve}, reject={Reject}) — round stays Pending (do-nothing). Approval is now stalled.",
                    context.ActivityId, totalMembers, approveCount, rejectCount);
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

    // Majority is evaluated against the FULL committee (totalMembers), not the votes cast. The string
    // MajorityConfig.Type is the round-tripped MajorityType name, so parse it back to the enum and
    // delegate to the shared domain rule (single source of truth). totalVotes is retained for
    // signature symmetry but is no longer the denominator; an unknown type yields false.
    private static bool CheckMajority(MajorityConfig config, int targetCount, int totalVotes, int totalMembers)
    {
        return Enum.TryParse<MajorityType>(config.Type, ignoreCase: true, out var majorityType)
            && MajorityRule.IsMet(majorityType, targetCount, totalMembers);
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

    private static Guid? ResolveAppraisalId(Dictionary<string, object>? variables)
    {
        if (variables is null || !variables.TryGetValue("appraisalId", out var raw))
            return null;

        return raw switch
        {
            Guid g => g,
            string s when Guid.TryParse(s, out var parsed) => parsed,
            JsonElement je when je.ValueKind == JsonValueKind.String
                                && Guid.TryParse(je.GetString(), out var jp) => jp,
            _ => null
        };
    }

    // Deserialization target for meetingMemberOverrides passed from MeetingActivity.
    // internal (not private) so System.Text.Json reflection can construct instances during variable rehydration.
    internal record MeetingMemberOverride(string UserId, string Role);
}
