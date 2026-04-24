namespace Workflow.Workflow.Events;

/// <summary>
/// Published by FanOutTaskActivity when N per-company tasks are created simultaneously.
/// Handler archives the previous single PendingTask (if any) and inserts one row per company.
/// </summary>
public record FanOutTasksAssignedEvent(
    Guid CorrelationId,
    string ActivityName,
    List<FanOutCompanyAssignment> Companies,
    DateTime AssignedAt,
    Guid WorkflowInstanceId,
    string ActivityId,
    DateTime? DueAt,
    string? StartedBy,
    string? WorkflowInstanceName,
    string? AppraisalNumber = null,
    string Movement = "F"
) : IDomainEvent;

public record FanOutCompanyAssignment(
    Guid CompanyId,
    string AssignedTo,
    string TaskName,
    string? TaskDescription = null);
