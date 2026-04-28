namespace Appraisal.Application.Features.Project.GetProjectUnitPrices;

/// <summary>Result containing the list of unit price DTOs for all project units.</summary>
public record GetProjectUnitPricesResult(
    List<ProjectUnitPriceDto> UnitPrices
);
