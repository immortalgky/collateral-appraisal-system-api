namespace Appraisal.Application.Features.Project.GetProjectTowerById;

/// <summary>Query to get a specific tower by its ID.</summary>
public record GetProjectTowerByIdQuery(Guid AppraisalId, Guid TowerId) : IQuery<GetProjectTowerByIdResult>;
