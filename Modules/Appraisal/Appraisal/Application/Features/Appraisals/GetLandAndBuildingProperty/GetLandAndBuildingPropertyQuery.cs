using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetLandAndBuildingProperty;

/// <summary>
/// Query to get a land and building property with its detail
/// </summary>
public record GetLandAndBuildingPropertyQuery(
    Guid AppraisalId,
    Guid PropertyId
) : IQuery<GetLandAndBuildingPropertyResult>;
