using Notification.Domain.Notifications.Dtos;

namespace Notification.Domain.Notifications.Features.GetWorkflowStatus;

public record GetWorkflowStatusResponse(
    long RequestId,
    string CurrentState,
    string? NextAssignee,
    string? NextAssigneeType,
    List<WorkflowStepDto> WorkflowSteps,
    DateTime UpdatedAt
);