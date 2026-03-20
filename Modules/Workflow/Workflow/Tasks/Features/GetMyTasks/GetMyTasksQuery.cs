using Shared.Pagination;
using Workflow.Tasks.Features.GetTasks;

namespace Workflow.Tasks.Features.GetMyTasks;

public record GetMyTasksFilterRequest(
    string? Status = null,
    string? Priority = null,
    string? TaskName = null
);

public record GetMyTasksQuery(
    PaginationRequest PaginationRequest,
    GetMyTasksFilterRequest? Filter = null
) : IQuery<GetMyTasksResult>;

public record GetMyTasksResult(PaginatedResult<TaskDto> Result);

public record GetMyTasksResponse(PaginatedResult<TaskDto> Result);
