namespace Appraisal.Application.Features.Project.GetProjectLand;

/// <summary>Query to get project land for a LandAndBuilding project.</summary>
public record GetProjectLandQuery(Guid AppraisalId) : IQuery<GetProjectLandResult>;
