namespace Request.Application.Features.RequestDocuments.GetRequestDocumentsByRequestId;

public record GetRequestDocumentsByRequestIdResult(
    int TotalDocuments,
    int TotalUploaded,
    List<DocumentSectionDto> Sections);

public record DocumentSectionDto(
    Guid? TitleId,
    string? TitleIdentifier,
    string? CollateralType,
    string? CollateralTypeName,
    string? SectionLabel,
    int TotalDocuments,
    int UploadedDocuments,
    List<DocumentItemDto> Documents);

public record DocumentItemDto(
    Guid Id,
    Guid? DocumentId,
    string? DocumentType,
    string? DocumentTypeName,
    string? FileName,
    string? FilePath,
    string? Notes,
    bool IsRequired,
    string? UploadedBy,
    string? UploadedByName,
    DateTime? UploadedAt);
