namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementBuildingProperty;

/// <summary>
/// Result of creating a lease agreement building property
/// </summary>
public record CreateLeaseAgreementBuildingPropertyResult(Guid PropertyId, Guid DetailId);
