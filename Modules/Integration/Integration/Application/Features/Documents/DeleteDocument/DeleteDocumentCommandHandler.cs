using Document.Domain.Documents;
using Shared.CQRS;

namespace Integration.Application.Features.Documents.DeleteDocument;

public class DeleteDocumentCommandHandler(
    IDocumentRepository documentRepository
) : ICommandHandler<DeleteDocumentCommand, DeleteDocumentResult>
{
    public async Task<DeleteDocumentResult> Handle(
        DeleteDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var document = await documentRepository.GetByIdAsync(command.DocumentId, cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException($"Document {command.DocumentId} not found");
        }

        // Mark document as deleted via repository
        await documentRepository.DeleteAsync(document, cancellationToken);

        return new DeleteDocumentResult(true);
    }
}
