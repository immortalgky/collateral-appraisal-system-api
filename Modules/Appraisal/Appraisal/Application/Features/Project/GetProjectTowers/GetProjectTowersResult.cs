namespace Appraisal.Application.Features.Project.GetProjectTowers;

/// <summary>Result containing all towers for a project.</summary>
public record GetProjectTowersResult(List<ProjectTowerDto> Towers);
