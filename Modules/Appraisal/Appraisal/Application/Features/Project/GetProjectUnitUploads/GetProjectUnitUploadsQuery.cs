namespace Appraisal.Application.Features.Project.GetProjectUnitUploads;

/// <summary>Query to get the upload history for a project.</summary>
public record GetProjectUnitUploadsQuery(Guid AppraisalId) : IQuery<GetProjectUnitUploadsResult>;
