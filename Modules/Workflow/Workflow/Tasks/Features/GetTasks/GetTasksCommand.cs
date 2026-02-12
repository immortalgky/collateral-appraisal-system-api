using Shared.Pagination;

namespace Workflow.Tasks.Features.GetTasks;

public sealed record GetTasksCommand() : ICommand<PaginatedResult<TaskItem>>;

public record TaskItem(
    Guid Id,
    string? AppraisalNumber,
    string? CustomerName,
    string? TaskType,
    string? Purpose,
    string? PropertyType,
    string? Status,
    DateTime? AppointmentDateTime,
    Guid? AssigneeUserId,
    DateTime? RequestedAt,
    DateTime? ReceivedDate,
    string? Movement,
    int SLADays,
    int OLAActual,
    int OLADiff,
    string? Priority
);