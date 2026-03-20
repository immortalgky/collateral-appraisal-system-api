namespace Workflow.Workflow.Features.GetEligibleAssignees;

public record GetEligibleAssigneesQuery(Guid WorkflowInstanceId, string ActivityId)
    : IRequest<GetEligibleAssigneesResponse>;

public record GetEligibleAssigneesResponse
{
    public string ActivityId { get; init; } = default!;
    public string? TeamId { get; init; }
    public string? TeamName { get; init; }
    public List<EligibleAssigneeDto> EligibleAssignees { get; init; } = [];
}

public record EligibleAssigneeDto(string UserId, string DisplayName);
