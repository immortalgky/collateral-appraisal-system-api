using Shared.CQRS;

namespace Common.Application.Features.Dashboard.GetAppraisalCounts;

public record GetAppraisalCountsQuery(
    string Period,
    DateOnly? From,
    DateOnly? To,
    bool GroupByType = false,
    string? BankingSegment = null
) : IQuery<GetAppraisalCountsResult>;

public record GetAppraisalCountsResult(List<AppraisalCountDto> Items);

public record AppraisalCountDto
{
    public string? Period { get; init; }
    // Null in overview mode; carries the appraisal type when GroupByType is requested.
    public string? AppraisalType { get; init; }
    public int CreatedCount { get; init; }
    public int CompletedCount { get; init; }
}
