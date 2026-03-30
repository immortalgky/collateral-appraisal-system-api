using Shared.CQRS;

namespace Common.Application.Features.Dashboard.GetRequestStatusSummary;

public record GetRequestStatusSummaryQuery : IQuery<GetRequestStatusSummaryResult>;

public record GetRequestStatusSummaryResult(List<RequestStatusDto> Items);

public record RequestStatusDto
{
    public string Status { get; init; } = default!;
    public int Count { get; init; }
}
