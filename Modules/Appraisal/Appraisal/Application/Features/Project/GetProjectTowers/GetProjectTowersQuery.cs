namespace Appraisal.Application.Features.Project.GetProjectTowers;

/// <summary>Query to get all towers for a Condo project.</summary>
public record GetProjectTowersQuery(Guid AppraisalId) : IQuery<GetProjectTowersResult>;
