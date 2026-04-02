namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementLandProperty;

/// <summary>
/// Result of creating a lease agreement land property
/// </summary>
public record CreateLeaseAgreementLandPropertyResult(Guid PropertyId, Guid LandDetailId);
