using Appraisal.Application.Features.Project.GetProjectTowers;

namespace Appraisal.Application.Features.Project.GetProjectTowerById;

/// <summary>Result containing a single tower DTO.</summary>
public record GetProjectTowerByIdResult(ProjectTowerDto Tower);
