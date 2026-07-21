namespace Appraisal.Application.Features.Appraisals.GetAppraisalDocuments;

public record GetAppraisalDocumentsResponse(
    int TotalTypes,
    int TypesWithFiles,
    List<AppraisalDocumentTypeResponse> Types);

public record AppraisalDocumentTypeResponse(
    string Code,
    string Name,
    string? NameTh,
    string? Category,
    int TotalFiles,
    List<AppraisalDocumentFileResponse> Files);

public record AppraisalDocumentFileResponse(
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
