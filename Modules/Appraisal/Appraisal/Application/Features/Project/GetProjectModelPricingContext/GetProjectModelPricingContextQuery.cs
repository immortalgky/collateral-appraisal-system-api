namespace Appraisal.Application.Features.Project.GetProjectModelPricingContext;

/// <summary>
/// Returns the flat pricing context for a specific project model.
/// Combines project-level, tower-level (Condo only), and model-level fields
/// needed to auto-populate factor subject values on the pricing analysis page.
/// </summary>
public record GetProjectModelPricingContextQuery(
    Guid AppraisalId,
    Guid ProjectId,
    Guid ModelId
) : IQuery<ProjectModelPricingContextDto>;
