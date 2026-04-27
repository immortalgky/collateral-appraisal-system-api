namespace Appraisal.Application.Features.Project.GetProjectUnitUploads;

/// <summary>DTO for a project unit upload batch.</summary>
public record ProjectUnitUploadDto(
    Guid Id,
    Guid ProjectId,
    string FileName,
    DateTime UploadedAt,
    bool IsUsed,
    Guid? DocumentId
);
