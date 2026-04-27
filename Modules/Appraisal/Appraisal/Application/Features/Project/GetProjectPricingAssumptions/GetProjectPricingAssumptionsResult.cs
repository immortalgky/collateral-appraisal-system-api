namespace Appraisal.Application.Features.Project.GetProjectPricingAssumptions;

/// <summary>Result containing the pricing assumptions DTO, or null if none have been saved yet.</summary>
public record GetProjectPricingAssumptionsResult(
    ProjectPricingAssumptionDto? Assumption
);
