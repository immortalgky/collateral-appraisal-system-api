using Workflow.Tasks.Features.GetMyTaskGroupCounts;
using Workflow.Tasks.Features.GetPoolTasks;

namespace Workflow.Tasks.Features.GetPoolTaskGroupCounts;

public record GetPoolTaskGroupCountsQuery(
    string GroupBy,
    GetPoolTasksFilterRequest? Filter = null
) : IQuery<GetPoolTaskGroupCountsResult>;

public record GetPoolTaskGroupCountsResult(IReadOnlyList<TaskGroupCountDto> Result);

public record GetPoolTaskGroupCountsResponse(IReadOnlyList<TaskGroupCountDto> Result);
