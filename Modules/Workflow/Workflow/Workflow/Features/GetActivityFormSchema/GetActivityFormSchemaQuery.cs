namespace Workflow.Workflow.Features.GetActivityFormSchema;

public record GetActivityFormSchemaQuery(Guid WorkflowInstanceId, string ActivityId)
    : IRequest<GetActivityFormSchemaResponse>;

public record GetActivityFormSchemaResponse
{
    public string ActivityId { get; init; } = default!;
    public string ActivityName { get; init; } = default!;
    public string ActivityType { get; init; } = default!;
    public List<ActionDto> Actions { get; init; } = [];
    public List<FormFieldDto> FormFields { get; init; } = [];
    public Dictionary<string, object?> CurrentValues { get; init; } = new();
}

public record FormFieldDto
{
    public string Name { get; init; } = default!;
    public string Label { get; init; } = default!;
    public string Type { get; init; } = "text"; // text, number, boolean, select, textarea
    public bool Required { get; init; }
    public string? DefaultValue { get; init; }
    public List<string>? Options { get; init; }
}

public record ActionDto
{
    public string Value { get; init; } = default!;
    public string Label { get; init; } = default!;
    public string AssignmentMode { get; init; } = "system";
    public string? TargetActivityId { get; set; }
}
