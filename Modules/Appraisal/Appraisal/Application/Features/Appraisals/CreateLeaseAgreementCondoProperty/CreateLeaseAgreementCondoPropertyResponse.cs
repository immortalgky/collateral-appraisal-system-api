namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementCondoProperty;

/// <summary>
/// Response returned after creating a lease agreement condo property
/// </summary>
public record CreateLeaseAgreementCondoPropertyResponse(Guid PropertyId, Guid DetailId);
