using Shared.CQRS;

namespace Integration.Application.Features.Documents.GetDocument;

public record GetDocumentQuery(Guid DocumentId) : IQuery<GetDocumentResult>;

public record GetDocumentResult(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string? DocumentType,
    string? Category,
    string DownloadUrl,
    DateTime CreatedOn
);
