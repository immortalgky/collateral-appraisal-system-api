namespace Appraisal.Application.Features.Project.GetProjectUnits;

/// <summary>Query to get all project units.</summary>
public record GetProjectUnitsQuery(Guid AppraisalId) : IQuery<GetProjectUnitsResult>;
