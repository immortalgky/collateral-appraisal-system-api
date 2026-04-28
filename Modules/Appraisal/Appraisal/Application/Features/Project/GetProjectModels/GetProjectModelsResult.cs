namespace Appraisal.Application.Features.Project.GetProjectModels;

/// <summary>Result containing all models for a project.</summary>
public record GetProjectModelsResult(List<ProjectModelDto> Models);
