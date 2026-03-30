using Shared.CQRS;

namespace Common.Application.Features.Dashboard.GetAppraisalCounts;

public record GetAppraisalCountsQuery(
    string Period,
    DateOnly? From,
    DateOnly? To
) : IQuery<GetAppraisalCountsResult>;

public record GetAppraisalCountsResult(List<AppraisalCountDto> Items);

public record AppraisalCountDto
{
    public string? Period { get; init; }
    public int CreatedCount { get; init; }
    public int CompletedCount { get; init; }
}
