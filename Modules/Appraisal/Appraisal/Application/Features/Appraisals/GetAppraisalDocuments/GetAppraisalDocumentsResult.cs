namespace Appraisal.Application.Features.Appraisals.GetAppraisalDocuments;

public record GetAppraisalDocumentsResult(
    int TotalTypes,
    int TypesWithFiles,
    List<AppraisalDocumentTypeDto> Types);

public record AppraisalDocumentTypeDto(
    string Code,
    string Name,
    string? NameTh,
    string? Category,
    int TotalFiles,
    List<AppraisalDocumentFileDto> Files);

public record AppraisalDocumentFileDto(
    Guid Id,
    Guid? DocumentId,
    string? FileName,
    string? MimeType,
    long? FileSizeBytes,
    string? Notes,
    int SortOrder,
    DateTime? UploadedAt,
    string? UploadedBy,
    string? UploadedByName);
