using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetVesselProperty;

/// <summary>
/// Query to get a vessel property with its detail
/// </summary>
public record GetVesselPropertyQuery(
    Guid AppraisalId,
    Guid PropertyId
) : IQuery<GetVesselPropertyResult>;
