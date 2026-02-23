using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetBuildingProperty;

/// <summary>
/// Query to get a building property with its detail
/// </summary>
public record GetBuildingPropertyQuery(
    Guid AppraisalId,
    Guid PropertyId
) : IQuery<GetBuildingPropertyResult>;
