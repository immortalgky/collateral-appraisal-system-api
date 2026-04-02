using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreementBuildingProperty;

/// <summary>
/// Query to get a lease agreement building property with its detail
/// </summary>
public record GetLeaseAgreementBuildingPropertyQuery(
    Guid AppraisalId,
    Guid PropertyId
) : IQuery<GetLeaseAgreementBuildingPropertyResult>;
