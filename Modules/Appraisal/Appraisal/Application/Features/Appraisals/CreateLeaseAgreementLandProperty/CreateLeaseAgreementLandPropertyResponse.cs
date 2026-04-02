namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementLandProperty;

/// <summary>
/// Response for creating a lease agreement land property
/// </summary>
public record CreateLeaseAgreementLandPropertyResponse(Guid PropertyId, Guid LandDetailId);
