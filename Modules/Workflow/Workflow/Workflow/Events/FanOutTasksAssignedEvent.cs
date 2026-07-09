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
    // No SlaDurationHours: FanOutTaskActivity derives DueAt from a workflow variable,
    // not the SLA policy engine, so there is no governing budget to stamp here. Fan-out
    // tasks intentionally omit the SLA budget chip.
) : IDomainEvent;

public record FanOutCompanyAssignment(
    Guid CompanyId,
    string AssignedTo,
    string TaskName,
    string? TaskDescription = null);
