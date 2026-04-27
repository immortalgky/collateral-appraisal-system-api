namespace Workflow.Tasks.Features.GetTaskCounts;

public record GetTaskCountsQuery() : IQuery<GetTaskCountsResult>;

public record GetTaskCountsResult(IReadOnlyList<ActivityTaskCountDto> Counts);

public record GetTaskCountsResponse(IReadOnlyList<ActivityTaskCountDto> Result);

public record ActivityTaskCountDto
{
    public string ActivityId { get; init; } = default!;
    public int MyCount { get; init; }
    public int PoolCount { get; init; }
}
