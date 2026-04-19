using Shared.CQRS;

namespace Common.Application.Features.Dashboard.GetAppraisalStatusSummary;

public record GetAppraisalStatusSummaryQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    string? AssigneeId = null,
    string? BankingSegment = null
) : IQuery<GetAppraisalStatusSummaryResult>;

public record GetAppraisalStatusSummaryResult(List<AppraisalStatusDto> Items);

public record AppraisalStatusDto
{
    public string Status { get; init; } = default!;
    public int Count { get; init; }
}
