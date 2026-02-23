namespace Appraisal.Application.Features.Appraisals.MovePropertyToGroup;

public record MovePropertyToGroupRequest(
    Guid TargetGroupId,
    int? TargetPosition
);
