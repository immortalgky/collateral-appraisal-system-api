namespace Appraisal.Application.Features.CommitteeVoting.GetApprovalList;

public record GetApprovalListResult(
    string? CommitteeName,
    string? ReviewStatus,
    Guid? ReviewId,
    IReadOnlyList<ApprovalListItem> Items
);

public record ApprovalListItem(
    Guid CommitteeMemberId,
    string MemberName,
    string Role,
    string? Vote,
    string VoteLabel,
    string? Remark,
    DateTime? VotedAt
);
