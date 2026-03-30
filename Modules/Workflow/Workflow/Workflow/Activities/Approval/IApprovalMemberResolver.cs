namespace Workflow.Workflow.Activities.Approval;

public interface IApprovalMemberResolver
{
    Task<ApprovalGroupInfo> ResolveMembersAsync(
        MemberSourceConfig config,
        Dictionary<string, object> variables,
        QuorumConfig? inlineQuorum,
        MajorityConfig? inlineMajority,
        CancellationToken ct = default);
}
