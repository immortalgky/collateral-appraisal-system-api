namespace Workflow.Services.Configuration.Models;

/// <summary>
/// Admin-facing shape of an <see cref="Data.Entities.AutoAssignmentRule"/>.
/// CSV condition fields are null/empty when the rule does not constrain that dimension.
/// </summary>
public record AutoAssignmentRuleDto(
    Guid Id,
    string RuleName,
    int Priority,
    bool IsActive,
    string? Channels,
    string? EntrySources,
    string? LoanTypes,
    string? Priorities,
    decimal? MinFacilityLimit,
    decimal? MaxFacilityLimit,
    string? ConditionExpression,
    string RoutingDecision,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CreatedBy,
    string UpdatedBy);

public class CreateAutoAssignmentRuleRequest
{
    public string RuleName { get; set; } = default!;
    public int Priority { get; set; }
    public string RoutingDecision { get; set; } = default!;
    public string? Channels { get; set; }
    public string? EntrySources { get; set; }
    public string? LoanTypes { get; set; }
    public string? Priorities { get; set; }
    public decimal? MinFacilityLimit { get; set; }
    public decimal? MaxFacilityLimit { get; set; }
    public string? ConditionExpression { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Set by the endpoint from the authenticated user; not accepted from the client.</summary>
    public string CreatedBy { get; set; } = "system";
}

public class UpdateAutoAssignmentRuleRequest
{
    public string RuleName { get; set; } = default!;
    public int Priority { get; set; }
    public string RoutingDecision { get; set; } = default!;
    public string? Channels { get; set; }
    public string? EntrySources { get; set; }
    public string? LoanTypes { get; set; }
    public string? Priorities { get; set; }
    public decimal? MinFacilityLimit { get; set; }
    public decimal? MaxFacilityLimit { get; set; }
    public string? ConditionExpression { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Set by the endpoint from the authenticated user; not accepted from the client.</summary>
    public string UpdatedBy { get; set; } = "system";
}
