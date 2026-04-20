namespace Workflow.Workflow.Events;

public record ApprovalTasksAssignedEvent(
    Guid CorrelationId,
    string ActivityName,
    List<ApprovalMemberAssignment> Members,
    DateTime AssignedAt,
    Guid WorkflowInstanceId,
    string ActivityId,
    DateTime? DueAt,
    string? StartedBy,
    string? WorkflowInstanceName,
    string? AppraisalNumber = null,
    string Movement = "F"
) : IDomainEvent;

public record ApprovalMemberAssignment(string Username, string TaskName, string? TaskDescription = null);
