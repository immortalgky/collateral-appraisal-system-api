namespace Appraisal.Application.Features.Project.GetProjectUnits;

/// <summary>Result containing all project units with summary lists.</summary>
public record GetProjectUnitsResult(
    List<ProjectUnitDto> Units,
    List<string> Towers,
    List<string> Models,
    int TotalCount
);
