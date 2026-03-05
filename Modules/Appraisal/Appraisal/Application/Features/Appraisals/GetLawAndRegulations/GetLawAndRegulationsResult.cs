namespace Appraisal.Application.Features.Appraisals.GetLawAndRegulations;

public record GetLawAndRegulationsResult(List<LawAndRegulationDto> Items);

public record LawAndRegulationDto(
    Guid Id,
    string HeaderCode,
    string? Remark,
    List<LawAndRegulationImageDto> Images
);

public record LawAndRegulationImageDto(
    Guid Id,
    Guid GalleryPhotoId,
    int DisplaySequence,
    string? Title,
    string? Description
);
