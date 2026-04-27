namespace Appraisal.Application.Features.Project.GetProjectModels;

/// <summary>Query to get all models for a project.</summary>
public record GetProjectModelsQuery(Guid AppraisalId) : IQuery<GetProjectModelsResult>;
