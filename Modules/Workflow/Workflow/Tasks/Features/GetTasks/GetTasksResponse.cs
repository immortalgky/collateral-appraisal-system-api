using Shared.Pagination;

namespace Workflow.Tasks.Features.GetTasks;

public record GetTasksResponse(PaginatedResult<TaskItem> Result);
