using Shared.Pagination;
using Workflow.Tasks.Features.GetTasks;
using Workflow.Tasks.Features.Shared;

namespace Workflow.Tasks.Features.GetPoolTasks;

public record GetPoolTasksFilterRequest(
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
    DateTime? AppointmentDateFrom = null,
    DateTime? AppointmentDateTo = null,
    DateTime? RequestedAtFrom = null,
    DateTime? RequestedAtTo = null,
    string? SortBy = null,
    string? SortDir = null,
    string? SlaStatus = null,
    string? ActivityId = null
) : ITaskListFilter;

public record GetPoolTasksQuery(
    PaginationRequest PaginationRequest,
    GetPoolTasksFilterRequest? Filter = null
) : IQuery<GetPoolTasksResult>;

public record GetPoolTasksResult(PaginatedResult<PoolTaskDto> Result);

public record GetPoolTasksResponse(PaginatedResult<PoolTaskDto> Result);

public record PoolTaskDto
{
    public Guid Id { get; init; }
    public Guid? AppraisalId { get; init; }
    public Guid TaskId { get; init; }
    public Guid WorkflowInstanceId { get; init; }
    public string? ActivityId { get; init; }
    public string? AppraisalNumber { get; init; }
    public string? CustomerName { get; init; }
    public string? TaskType { get; init; }
    public string? TaskDescription { get; init; }
    public string? Purpose { get; init; }
    public string? PropertyType { get; init; }
    public string? Status { get; init; }
    public string? PendingTaskStatus { get; init; }
    public DateTime? AppointmentDateTime { get; init; }
    public string? AssigneeUserId { get; init; }
    public string? RequestedBy { get; init; }
    public DateTime? RequestReceivedDate { get; init; }
    public DateTime? AssignedDate { get; init; }
    public string? Movement { get; init; }
    public string? InternalFollowupStaff { get; init; }
    public string? Appraiser { get; init; }
    public string? Priority { get; init; }
    public DateTime? DueAt { get; init; }
    public string? SlaStatus { get; init; }
    public int? ElapsedHours { get; init; }
    public int? RemainingHours { get; init; }
    public string? WorkingBy { get; init; }
    public DateTime? LockedAt { get; init; }
}
