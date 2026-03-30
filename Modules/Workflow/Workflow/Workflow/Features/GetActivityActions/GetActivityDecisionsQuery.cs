namespace Workflow.Workflow.Features.GetActivityActions;

public record GetActivityActionsQuery(Guid WorkflowInstanceId, string ActivityId)
    : IRequest<GetActivityActionsResponse>;

public record GetActivityActionsResponse
{
    public string ActivityId { get; init; } = default!;
    public string ActivityName { get; init; } = default!;
    public List<ActionDto> Actions { get; init; } = [];
}

public record ActionDto
{
    public string Value { get; init; } = default!;
    public string Label { get; init; } = default!;
    public string AssignmentMode { get; init; } = "system";
    public string? TargetActivityId { get; set; }
}
