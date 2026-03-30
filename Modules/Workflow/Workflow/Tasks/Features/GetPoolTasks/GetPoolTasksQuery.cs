using Shared.Pagination;
using Workflow.Tasks.Features.GetTasks;

namespace Workflow.Tasks.Features.GetPoolTasks;

public record GetPoolTasksFilterRequest(
    string? Status = null,
    string? Priority = null,
    string? TaskName = null
);

public record GetPoolTasksQuery(
    PaginationRequest PaginationRequest,
    GetPoolTasksFilterRequest? Filter = null
) : IQuery<GetPoolTasksResult>;

public record GetPoolTasksResult(PaginatedResult<PoolTaskDto> Result);

public record GetPoolTasksResponse(PaginatedResult<PoolTaskDto> Result);

public record PoolTaskDto
{
    public Guid Id { get; init; }
    public Guid WorkflowInstanceId { get; init; }
    public string? ActivityId { get; init; }
    public string? AppraisalNumber { get; init; }
    public string? CustomerName { get; init; }
    public string? TaskType { get; init; }
    public string? TaskDescription { get; init; }
    public string? Purpose { get; init; }
    public string? PropertyType { get; init; }
    public string? Status { get; init; }
    public DateTime? AppointmentDateTime { get; init; }
    public string? AssignedTo { get; init; }
    public string? AssignedType { get; init; }
    public string? WorkingBy { get; init; }
    public DateTime? RequestedAt { get; init; }
    public DateTime? ReceivedDate { get; init; }
    public string? Movement { get; init; }
    public int? SLADays { get; init; }
    public int? OLAActual { get; init; }
    public int? OLADiff { get; init; }
    public string? Priority { get; init; }
}
