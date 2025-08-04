using Document.Services;

namespace Document.Documents.Features.DeleteDocument;

internal class DeleteDocumentHandler(IDocumentService documentService)
    : ICommandHandler<DeleteDocumentCommand, DeleteDocumentResult>
{
    public async Task<DeleteDocumentResult> Handle(DeleteDocumentCommand command, CancellationToken cancellationToken)
    {
        var result = await documentService.DeleteFileAsync(command.Id, cancellationToken);
        
        return new DeleteDocumentResult(result);
    }
}