using Shared.Pagination;
using Workflow.Tasks.Features.GetTasks;

namespace Workflow.Tasks.Features.GetMyTasks;

public record GetMyTasksFilterRequest(
    string? ActivityId = null,
    string? Status = null,
    string? Priority = null,
    string? TaskName = null,
    string? Search = null,
    string? AppraisalNumber = null,
    string? CustomerName = null,
    string? TaskStatus = null,
    string? TaskType = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? SortBy = null,
    string? SortDir = null
);

public record GetMyTasksQuery(
    PaginationRequest PaginationRequest,
    GetMyTasksFilterRequest? Filter = null
) : IQuery<GetMyTasksResult>;

public record GetMyTasksResult(PaginatedResult<TaskDto> Result);

public record GetMyTasksResponse(PaginatedResult<TaskDto> Result);