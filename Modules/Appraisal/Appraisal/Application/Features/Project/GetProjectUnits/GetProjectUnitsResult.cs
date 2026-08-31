namespace Appraisal.Application.Features.Project.GetProjectUnits;

/// <summary>Result containing all project units with summary lists.</summary>
public record GetProjectUnitsResult(
    List<ProjectUnitDto> Units,
    List<string> Towers,
    List<string> Models,
    // Every unit of the project, sold included — what "Total Units" on the listing means.
    int TotalCount,
    // The subset still to be sold. Kept separate because the listing reports both, and deriving
    // it in the client would drift the moment the list is paged or filtered.
    int RemainingCount
);
