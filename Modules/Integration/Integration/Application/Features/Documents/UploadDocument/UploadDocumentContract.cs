namespace Integration.Application.Features.Documents.UploadDocument;

public sealed record UploadDocumentResponse(
    Guid DocumentId,
    string FileName,
    string ContentType,
    long SizeInBytes
);