using Document.Services;
using Shared.CQRS;

namespace Document.Domain.Documents.Features.DeleteDocument;

internal class DeleteDocumentHandler(IDocumentService documentService)
    : ICommandHandler<DeleteDocumentCommand, DeleteDocumentResult>
{
    public async Task<DeleteDocumentResult> Handle(DeleteDocumentCommand command, CancellationToken cancellationToken)
    {
        var result = await documentService.DeleteFileAsync(command.Id, cancellationToken);

        return new DeleteDocumentResult(result);
    }
}