namespace Appraisal.Application.Features.Appraisals.LinkPhotoToProperty;

public record LinkPhotoToPropertyRequest(
    Guid AppraisalPropertyId,
    string PhotoPurpose,
    string? SectionReference,
    string LinkedBy
);
