using Shared.CQRS;

namespace Integration.Application.Features.Documents.DeleteDocument;

public record DeleteDocumentCommand(Guid DocumentId) : ICommand<DeleteDocumentResult>;

public record DeleteDocumentResult(bool Success);
