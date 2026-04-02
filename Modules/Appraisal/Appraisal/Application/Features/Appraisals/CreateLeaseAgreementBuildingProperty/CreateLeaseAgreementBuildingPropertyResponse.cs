namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementBuildingProperty;

/// <summary>
/// Response returned after creating a lease agreement building property
/// </summary>
public record CreateLeaseAgreementBuildingPropertyResponse(Guid PropertyId, Guid DetailId);
