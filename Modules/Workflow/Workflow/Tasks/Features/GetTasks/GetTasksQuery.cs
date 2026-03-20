using Shared.Pagination;

namespace Workflow.Tasks.Features.GetTasks;

public record GetTasksFilterRequest(
    string? Status = null,
    string? AssigneeUserId = null,
    string? Priority = null,
    string? TaskName = null
);

public record GetTasksQuery(
    PaginationRequest PaginationRequest,
    GetTasksFilterRequest? Filter = null
) : IQuery<GetTasksResult>;
