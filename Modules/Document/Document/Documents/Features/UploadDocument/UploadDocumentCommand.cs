using Shared.CQRS;

namespace Document.Documents.Features.UploadDocument;

public record UploadDocumentCommand(
    IFormFile File,
    Guid UploadSessionId,
    string DocumentType,
    string DocumentCategory,
    string? Description
) : ICommand<UploadDocumentResult>, ITransactionalCommand<IDocumentUnitOfWork>;