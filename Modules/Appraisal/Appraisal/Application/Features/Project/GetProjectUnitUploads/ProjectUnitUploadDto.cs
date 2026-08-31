namespace Appraisal.Application.Features.Project.GetProjectUnitUploads;

/// <summary>DTO for a project unit upload batch.</summary>
public record ProjectUnitUploadDto(
    Guid Id,
    Guid ProjectId,
    string FileName,
    DateTime UploadedAt,
    bool IsUsed,
    Guid? DocumentId,
    // Seeded by the system rather than uploaded by a person.
    bool IsSystemGenerated,
    // What the batch did. AddedUnits applies to every kind; the other three are re-match only and
    // are null elsewhere.
    int AddedUnits,
    int? MatchedUnsoldUnits,
    int? AutoSoldUnits,
    int? UpdatedUnits
);
