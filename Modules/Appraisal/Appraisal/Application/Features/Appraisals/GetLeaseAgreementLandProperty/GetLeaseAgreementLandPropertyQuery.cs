using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreementLandProperty;

/// <summary>
/// Query to get a lease agreement land property by ID
/// </summary>
public record GetLeaseAgreementLandPropertyQuery(Guid AppraisalId, Guid PropertyId) : IQuery<GetLeaseAgreementLandPropertyResult>;
