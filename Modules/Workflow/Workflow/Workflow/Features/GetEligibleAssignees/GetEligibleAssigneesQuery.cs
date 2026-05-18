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

public record EligibleAssigneeDto(string UserId, string DisplayName)
{
    /// <summary>
    /// Same as UserId in this codebase — included for FE clarity (UserId is the AspNet UserName).
    /// </summary>
    public string UserName { get; init; } = default!;

    /// <summary>
    /// Role descriptions the user belongs to (from auth.AspNetRoles.Description, falls back to Name).
    /// </summary>
    public List<string> Roles { get; init; } = [];
}
