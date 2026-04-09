using Shared.Pagination;

namespace Workflow.Tasks.Features.GetTasks;

public record GetTasksFilterRequest(
    string? Status = null,
    string? AssigneeUserId = null,
    string? Priority = null,
    string? TaskName = null,
    string? ActivityId = null,
    string? AppraisalNumber = null,
    string? CustomerName = null,
    string? TaskStatus = null,
    string? TaskType = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? SortBy = null,
    string? SortDir = null
);

public record GetTasksQuery(
    PaginationRequest PaginationRequest,
    GetTasksFilterRequest? Filter = null
) : IQuery<GetTasksResult>;
