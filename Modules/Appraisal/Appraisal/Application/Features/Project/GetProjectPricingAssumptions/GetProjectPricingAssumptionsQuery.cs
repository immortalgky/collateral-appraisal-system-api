namespace Appraisal.Application.Features.Project.GetProjectPricingAssumptions;

/// <summary>Query to retrieve pricing assumptions for a project.</summary>
public record GetProjectPricingAssumptionsQuery(
    Guid AppraisalId
) : IQuery<GetProjectPricingAssumptionsResult>;
