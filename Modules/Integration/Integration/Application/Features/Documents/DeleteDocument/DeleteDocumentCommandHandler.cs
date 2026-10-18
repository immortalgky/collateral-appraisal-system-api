using Document.Services;
using Shared.CQRS;

namespace Integration.Application.Features.Documents.DeleteDocument;

// One delete path for both endpoints: the Document module's service owns the rule (soft delete only,
// 409 while anything links the file, 404 for an unknown id, repeat deletes are no-ops).
public class DeleteDocumentCommandHandler(IDocumentService documentService)
    : ICommandHandler<DeleteDocumentCommand, DeleteDocumentResult>
{
    public async Task<DeleteDocumentResult> Handle(
        DeleteDocumentCommand command,
        CancellationToken cancellationToken)
    {
        await documentService.DeleteFileAsync(command.DocumentId, cancellationToken);

        return new DeleteDocumentResult(true);
    }
}
