using Notification.Notification.Dtos;

namespace Notification.Notification.Features.GetWorkflowStatus;

public record GetWorkflowStatusResponse(
    long RequestId,
    string CurrentState,
    string? NextAssignee,
    string? NextAssigneeType,
    List<WorkflowStepDto> WorkflowSteps,
    DateTime UpdatedAt
);