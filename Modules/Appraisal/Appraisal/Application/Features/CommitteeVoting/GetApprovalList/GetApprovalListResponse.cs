namespace Appraisal.Application.Features.CommitteeVoting.GetApprovalList;

public record GetApprovalListResponse(
    string? CommitteeName,
    string? ReviewStatus,
    Guid? ReviewId,
    IReadOnlyList<ApprovalListItem> Items
);
