using Shared.Pagination;
using Workflow.Tasks.Features.Shared;

namespace Workflow.Tasks.Features.GetTasks;

public record GetTasksFilterRequest(
    string? Status = null,
    string? AssigneeUserId = null,
    string? Priority = null,
    string? TaskName = null,
    string? ActivityId = null,
    string? AppraisalNumber = null,
    string? Channel = null,
    string? CustomerName = null,
    string? TaskStatus = null,
    string? TaskType = null,
    string? Purpose = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? SortBy = null,
    string? SortDir = null,
    string? SlaStatus = null,
    string? Search = null,
    DateTime? AppointmentDateFrom = null,
    DateTime? AppointmentDateTo = null,
    DateTime? RequestedAtFrom = null,
    DateTime? RequestedAtTo = null,
    string? TaskStatusBucket = null
) : ITaskListFilter;

public record GetTasksQuery(
    PaginationRequest PaginationRequest,
    GetTasksFilterRequest? Filter = null
) : IQuery<GetTasksResult>;
