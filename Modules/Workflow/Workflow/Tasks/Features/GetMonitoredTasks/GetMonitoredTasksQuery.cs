using Shared.Pagination;

namespace Workflow.Tasks.Features.GetMonitoredTasks;

public record GetMonitoredTasksQuery(
    PaginationRequest PaginationRequest,
    GetMonitoredTasksFilter? Filter = null
) : IQuery<GetMonitoredTasksResult>;

public record GetMonitoredTasksFilter(
    string[]? GroupId = null,
    string[]? AssigneeUsername = null,
    string[]? SlaStatus = null,
    string[]? ActivityId = null,
    string? Search = null,
    string? AppraisalNumber = null,
    string? CustomerName = null,
    string[]? AppraisalStatus = null,
    string[]? TaskType = null,
    string? SortBy = null,
    string? SortDir = null
);

public record GetMonitoredTasksResult(PaginatedResult<MonitoredTaskDto> Result);

public record GetMonitoredTasksResponse(PaginatedResult<MonitoredTaskDto> Result);

public record MonitoredTaskDto
{
    public Guid TaskId { get; init; }
    public Guid? AppraisalId { get; init; }
    public Guid WorkflowInstanceId { get; init; }
    public string? ActivityId { get; init; }
    public string? ActivityName { get; init; }
    public string? AppraisalNumber { get; init; }
    public string? PrevAppraisalNumber { get; init; }
    public string? CustomerName { get; init; }
    public string? Purpose { get; init; }
    public decimal? FacilityLimit { get; init; }
    public string? TaskName { get; init; }
    public string? TaskDescription { get; init; }
    public string? AssignedTo { get; init; }
    public string? AssignedToDisplayName { get; init; }
    public Guid? GroupId { get; init; }
    public string? GroupName { get; init; }
    public string? TaskStatus { get; init; }
    public string? AppraisalStatus { get; init; }
    public DateTime? AssignedAt { get; init; }
    public DateTime? DueAt { get; init; }
    public DateTime? SlaStartAt { get; init; }
    public string? SlaStatus { get; init; }
    public int? ElapsedHours { get; init; }
    public int? RemainingHours { get; init; }
    public string? WorkingBy { get; init; }
    public DateTime? LockedAt { get; init; }
}
