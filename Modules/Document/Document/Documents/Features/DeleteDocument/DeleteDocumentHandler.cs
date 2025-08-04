namespace Document.Documents.Features.DeleteDocument;

internal class DeleteDocumentHandler(IDocumentRepository documentRepository)
    : ICommandHandler<DeleteDocumentCommand, DeleteDocumentResult>
{
    public async Task<DeleteDocumentResult> Handle(DeleteDocumentCommand command, CancellationToken cancellationToken)
    {
        var file = await documentRepository.GetDocumentById(command.Id, false, cancellationToken);

        if (!File.Exists(file.FilePath)) throw new DocumentNotFoundException(file.Id);

        File.Delete(file.FilePath);
        
        var result = await documentRepository.DeleteDocument(command.Id, cancellationToken);

        return new DeleteDocumentResult(result);
    }
}