using Workflow.Tasks.Features.GetMyTasks;

namespace Workflow.Tasks.Features.GetMyTaskGroupCounts;

public record GetMyTaskGroupCountsQuery(
    string GroupBy,
    GetMyTasksFilterRequest? Filter = null
) : IQuery<GetMyTaskGroupCountsResult>;

public record TaskGroupCountDto(string Value, int Count);

public record GetMyTaskGroupCountsResult(IReadOnlyList<TaskGroupCountDto> Result);

public record GetMyTaskGroupCountsResponse(IReadOnlyList<TaskGroupCountDto> Result);
