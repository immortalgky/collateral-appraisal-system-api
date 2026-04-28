namespace Appraisal.Application.Features.Project.GetProjectUnitPrices;

/// <summary>Query to retrieve unit prices for all units in a project.</summary>
public record GetProjectUnitPricesQuery(
    Guid AppraisalId
) : IQuery<GetProjectUnitPricesResult>;
