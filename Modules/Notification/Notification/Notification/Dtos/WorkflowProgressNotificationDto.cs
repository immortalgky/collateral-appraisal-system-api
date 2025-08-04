namespace Notification.Notification.Dtos;

public record WorkflowProgressNotificationDto(
    Guid CorrelationId,
    long RequestId,
    string CurrentState,
    string? NextAssignee,
    string? NextAssigneeType,
    List<WorkflowStepDto> WorkflowSteps,
    DateTime UpdatedAt
);

public record WorkflowStepDto(
    string StateName,
    bool IsCompleted,
    bool IsCurrent,
    string? AssignedTo,
    DateTime? CompletedAt
);