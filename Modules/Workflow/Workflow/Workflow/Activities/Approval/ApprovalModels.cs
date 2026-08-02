namespace Workflow.Workflow.Activities.Approval;

public record MemberSourceConfig(
    string Type,
    List<string>? Members,
    string? CommitteeCode,
    Guid? CommitteeId,
    string? ValueExpression,
    List<ThresholdConfig>? Thresholds);

public record ThresholdConfig(decimal? MaxValue, string CommitteeCode);

public record QuorumConfig(string Type, int Value);

/// <param name="Value">
/// Only meaningful for <see cref="Workflow.Domain.Committees.MajorityType.FixedCount"/>. Defaults
/// to 0 so every existing call site and every already-persisted <c>{activityId}_majority</c>
/// workflow variable (these round-trip through JSON) keeps deserializing unchanged.
/// </param>
public record MajorityConfig(string Type, string TargetVote, int Value = 0);

public record ApprovalGroupInfo(
    List<ApprovalMemberInfo> Members,
    QuorumConfig Quorum,
    MajorityConfig Majority,
    List<ApprovalConditionInfo> Conditions,
    string? CommitteeName,
    string? CommitteeCode,
    // Defaults to "Quorum" to preserve the pre-existing early-decide behavior for inline approval
    // groups; committee-backed groups override this from Committee.VotingMode (seeded WaitForAll).
    string VotingMode = "Quorum");

public record ApprovalMemberInfo(string Username, string? Role);

public record ApprovalConditionInfo(string ConditionType, string? RoleRequired, int? MinVotesRequired);
