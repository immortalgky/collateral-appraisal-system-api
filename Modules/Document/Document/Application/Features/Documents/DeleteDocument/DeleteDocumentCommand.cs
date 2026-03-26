using Shared.CQRS;

namespace Document.Domain.Documents.Features.DeleteDocument;

public record DeleteDocumentCommand(Guid Id)
    : ICommand<DeleteDocumentResult>, ITransactionalCommand<IDocumentUnitOfWork>;