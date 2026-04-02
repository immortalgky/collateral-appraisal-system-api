using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreementLandAndBuildingProperty;

/// <summary>
/// Query to get a lease agreement land and building property with its detail
/// </summary>
public record GetLeaseAgreementLandAndBuildingPropertyQuery(
    Guid AppraisalId,
    Guid PropertyId
) : IQuery<GetLeaseAgreementLandAndBuildingPropertyResult>;
