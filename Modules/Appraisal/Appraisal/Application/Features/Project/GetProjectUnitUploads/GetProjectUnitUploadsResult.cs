namespace Appraisal.Application.Features.Project.GetProjectUnitUploads;

/// <summary>Result containing upload history for a project.</summary>
public record GetProjectUnitUploadsResult(List<ProjectUnitUploadDto> Uploads);
