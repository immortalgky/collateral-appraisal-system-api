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

public record MajorityConfig(string Type, string TargetVote);

public record ApprovalGroupInfo(
    List<ApprovalMemberInfo> Members,
    QuorumConfig Quorum,
    MajorityConfig Majority,
    List<ApprovalConditionInfo> Conditions,
    string? CommitteeName,
    string? CommitteeCode);

public record ApprovalMemberInfo(string Username, string? Role);

public record ApprovalConditionInfo(string ConditionType, string? RoleRequired, int? MinVotesRequired);
