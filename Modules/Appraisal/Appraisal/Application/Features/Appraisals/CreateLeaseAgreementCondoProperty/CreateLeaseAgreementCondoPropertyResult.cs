namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementCondoProperty;

/// <summary>
/// Result of creating a lease agreement condo property
/// </summary>
public record CreateLeaseAgreementCondoPropertyResult(Guid PropertyId, Guid DetailId);
