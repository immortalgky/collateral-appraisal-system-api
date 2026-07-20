namespace Appraisal.Application.Features.Appraisals.AddAppraisalDocument;

public record AddAppraisalDocumentRequest(
    string DocumentTypeCode,
    Guid DocumentId,
    string FileName,
    string? MimeType,
    long? FileSizeBytes,
    string? Notes,
    int? SortOrder,
    string? UploadedByName);
