namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementLandAndBuildingProperty;

/// <summary>
/// Response returned after creating a lease agreement land and building property
/// </summary>
public record CreateLeaseAgreementLandAndBuildingPropertyResponse(Guid PropertyId, Guid DetailId);
