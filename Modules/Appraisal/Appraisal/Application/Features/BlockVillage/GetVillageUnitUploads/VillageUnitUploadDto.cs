namespace Appraisal.Application.Features.BlockVillage.GetVillageUnitUploads;

public record VillageUnitUploadDto(
    Guid Id,
    Guid AppraisalId,
    string FileName,
    DateTime UploadedAt,
    bool IsUsed,
    Guid? DocumentId
);
