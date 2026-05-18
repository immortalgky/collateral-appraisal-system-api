using Shared.Pagination;

namespace Workflow.Tasks.Features.GetMonitoredPeople;

public record GetMonitoredPeopleQuery(
    PaginationRequest PaginationRequest,
    GetMonitoredPeopleFilter? Filter = null
) : IQuery<GetMonitoredPeopleResult>;

public record GetMonitoredPeopleFilter(
    string? Search = null,
    string? SortBy = null,
    string? SortDir = null
);

public record GetMonitoredPeopleResult(PaginatedResult<MonitoredPersonDto> Result);

public record GetMonitoredPeopleResponse(PaginatedResult<MonitoredPersonDto> Result);

public record MonitoredPersonDto
{
    public string UserName { get; init; } = default!;
    public string? DisplayName { get; init; }
    public int OpenTasks { get; init; }
    public int AvailableTasks { get; init; }
    public int TotalTasks { get; init; }
}
