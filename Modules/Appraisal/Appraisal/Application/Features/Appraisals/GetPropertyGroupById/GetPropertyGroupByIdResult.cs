namespace Appraisal.Application.Features.Appraisals.GetPropertyGroupById;

/// <summary>
/// Result of getting a property group by ID. <see cref="Readiness"/> tells the
/// React client whether the "Analyze Price" button should be enabled and, if not,
/// which rules failed (so chips/tooltips can be rendered next to each property).
/// </summary>
public record GetPropertyGroupByIdResult(
    Guid Id,
    int GroupNumber,
    string GroupName,
    string? Description,
    Guid? PricingAnalysisId,
    List<PropertyGroupItemDto>? Properties
)
{
    public PricingAnalysisReadinessDto? Readiness { get; set; }
}

/// <summary>
/// Read-side projection of the four pricing-analysis preconditions.
/// Mirrors the wire shape returned on 422 from CreatePricingAnalysis.
/// </summary>
public record PricingAnalysisReadinessDto(
    bool CanStartPricingAnalysis,
    IReadOnlyList<RuleViolationDto> Violations);

public record RuleViolationDto(
    string Code,
    string Message,
    Guid? PropertyId);
