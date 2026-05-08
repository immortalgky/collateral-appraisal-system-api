using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreementCondoProperty;

/// <summary>
/// Query to get a lease agreement condo property with its detail
/// </summary>
public record GetLeaseAgreementCondoPropertyQuery(
    Guid AppraisalId,
    Guid PropertyId
) : IQuery<GetLeaseAgreementCondoPropertyResult>;
