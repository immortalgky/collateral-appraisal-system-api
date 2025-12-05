namespace Document.Documents.Features.UploadDocument;

public record UploadDocumentResult(
    bool IsSuccess,
    Guid DocumentId,
    string FileName,
    long FileSize
);