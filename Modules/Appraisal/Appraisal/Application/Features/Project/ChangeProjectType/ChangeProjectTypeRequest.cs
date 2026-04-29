namespace Appraisal.Application.Features.Project.ChangeProjectType;

/// <summary>
/// HTTP request body for changing a project's type.
/// </summary>
public record ChangeProjectTypeRequest(ProjectType NewProjectType);
