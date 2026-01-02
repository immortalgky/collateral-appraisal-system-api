using Shared.CQRS;

namespace Document.Domain.Documents.Features.DeleteDocument;

public record DeleteDocumentCommand(long Id) : ICommand<DeleteDocumentResult>;