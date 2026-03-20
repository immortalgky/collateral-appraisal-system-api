namespace Appraisal.Application.Features.Appraisals.GetAppraisalAppendices;

public record GetAppraisalAppendicesResult(List<AppraisalAppendixDto> Items);

public record AppraisalAppendixDto(
    Guid Id,
    Guid AppendixTypeId,
    string AppendixTypeCode,
    string AppendixTypeName,
    int SortOrder,
    int LayoutColumns,
    List<AppendixDocumentDto> Documents
);

public record AppendixDocumentDto(
    Guid Id,
    Guid GalleryPhotoId,
    Guid DocumentId,
    int DisplaySequence,
    string? FileName,
    string? FilePath,
    string? FileExtension,
    string? MimeType,
    long? FileSizeBytes
);
