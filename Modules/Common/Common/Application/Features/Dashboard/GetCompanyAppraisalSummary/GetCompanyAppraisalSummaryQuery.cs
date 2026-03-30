using Shared.CQRS;

namespace Common.Application.Features.Dashboard.GetCompanyAppraisalSummary;

public record GetCompanyAppraisalSummaryQuery : IQuery<GetCompanyAppraisalSummaryResult>;

public record GetCompanyAppraisalSummaryResult(List<CompanyAppraisalSummaryDto> Items);

public record CompanyAppraisalSummaryDto
{
    public Guid CompanyId { get; init; }
    public string CompanyName { get; init; } = default!;
    public int AssignedCount { get; init; }
    public int CompletedCount { get; init; }
}
