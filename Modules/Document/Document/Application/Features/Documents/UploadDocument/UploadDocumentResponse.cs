namespace Document.Domain.Documents.Features.UploadDocument;

public record UploadDocumentResponse(
    bool IsSuccess,
    Guid DocumentId,
    string FileName,
    long FileSize
);
