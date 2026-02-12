using Shared.Pagination;

namespace Workflow.Tasks.Features.GetTasks;

public record GetTasksResult(PaginatedResult<TaskItem> Result);
