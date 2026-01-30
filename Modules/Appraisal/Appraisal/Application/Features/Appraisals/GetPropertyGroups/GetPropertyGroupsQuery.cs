using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetPropertyGroups;

/// <summary>
/// Query to get all property groups for an appraisal
/// </summary>
public record GetPropertyGroupsQuery(
    Guid AppraisalId
) : IQuery<GetPropertyGroupsResult>;
