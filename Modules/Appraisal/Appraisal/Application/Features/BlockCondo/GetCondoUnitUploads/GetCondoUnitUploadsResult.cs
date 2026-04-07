namespace Appraisal.Application.Features.BlockCondo.GetCondoUnitUploads;

public record GetCondoUnitUploadsResult(IReadOnlyList<CondoUnitUploadDto> Uploads);

public record CondoUnitUploadDto(
    Guid Id,
    Guid AppraisalId,
    string FileName,
    DateTime UploadedAt,
    bool IsUsed,
    Guid? DocumentId
);
