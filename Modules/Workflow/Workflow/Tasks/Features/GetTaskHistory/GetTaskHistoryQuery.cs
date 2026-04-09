using Shared.CQRS;

namespace Workflow.Tasks.Features.GetTaskHistory;

public record GetTaskHistoryQuery(Guid WorkflowInstanceId) : IQuery<GetTaskHistoryResponse>;

public record GetTaskHistoryResponse(IReadOnlyList<TaskHistoryItemDto> Items);

public record TaskHistoryItemDto
{
    public Guid TaskId { get; init; }
    public string TaskName { get; init; } = default!;
    public string? TaskDescription { get; init; }
    public string AssignedTo { get; init; } = default!;
    public string? AssignedToFirstName { get; init; }
    public string? AssignedToLastName { get; init; }
    public string? AssignedToDisplayName { get; init; }
    public string AssignedType { get; init; } = default!;
    public DateTime AssignedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ActionTaken { get; init; }
    public string? Remark { get; init; }
}
