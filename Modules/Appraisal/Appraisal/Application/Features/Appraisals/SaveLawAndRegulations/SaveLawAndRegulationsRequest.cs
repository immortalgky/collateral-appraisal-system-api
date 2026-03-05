namespace Appraisal.Application.Features.Appraisals.SaveLawAndRegulations;

public record SaveLawAndRegulationsRequest(
    List<LawAndRegulationItemInput> Items
);

public record LawAndRegulationItemInput(
    Guid? Id,
    string HeaderCode,
    string? Remark,
    List<LawAndRegulationImageInput> Images
);

public record LawAndRegulationImageInput(
    Guid? Id,
    Guid GalleryPhotoId,
    int DisplaySequence,
    string? Title,
    string? Description
);
