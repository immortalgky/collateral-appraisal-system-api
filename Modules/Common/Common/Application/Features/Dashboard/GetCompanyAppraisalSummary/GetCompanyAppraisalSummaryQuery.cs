using Shared.CQRS;

namespace Common.Application.Features.Dashboard.GetCompanyAppraisalSummary;

public record GetCompanyAppraisalSummaryQuery(
    DateOnly? From = null,
    DateOnly? To = null
) : IQuery<GetCompanyAppraisalSummaryResult>;

public record GetCompanyAppraisalSummaryResult(List<CompanyAppraisalSummaryDto> Items);

public record CompanyAppraisalSummaryDto
{
    public Guid CompanyId { get; init; }
    public string CompanyName { get; init; } = default!;
    /// <summary>Thai name; null when the company has none. The client picks by its own locale.</summary>
    public string? CompanyNameLocal { get; init; }
    public int AssignedCount { get; init; }
    public int CompletedCount { get; init; }
    public int OverdueCount { get; init; }
    public int InProgressCount { get; init; }
}
