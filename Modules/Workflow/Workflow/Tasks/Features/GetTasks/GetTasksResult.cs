using Shared.Pagination;

namespace Workflow.Tasks.Features.GetTasks;

public record GetTasksResult(PaginatedResult<TaskDto> Result);

public record TaskDto
{
    public Guid Id { get; init; }
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
    public DateTime? AppointmentDateTime { get; init; }
    public string? AssigneeUserId { get; init; }
    public DateTime? RequestedAt { get; init; }
    public DateTime? ReceivedDate { get; init; }
    public string? Movement { get; init; }
    public int? SLADays { get; init; }
    public int? OLAActual { get; init; }
    public int? OLADiff { get; init; }
    public string? Priority { get; init; }
    public DateTime? DueAt { get; init; }
    public string? SlaStatus { get; init; }
    public int? ElapsedHours { get; init; }
    public int? RemainingHours { get; init; }
}