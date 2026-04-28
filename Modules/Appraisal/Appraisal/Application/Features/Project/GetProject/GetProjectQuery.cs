namespace Appraisal.Application.Features.Project.GetProject;

/// <summary>Query to get the project for an appraisal.</summary>
public record GetProjectQuery(Guid AppraisalId) : IQuery<GetProjectResult?>;
