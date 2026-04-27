namespace Appraisal.Application.Features.Project.GetProjectModelById;

/// <summary>Query to get a specific project model by its ID.</summary>
public record GetProjectModelByIdQuery(Guid AppraisalId, Guid ModelId) : IQuery<GetProjectModelByIdResult>;
