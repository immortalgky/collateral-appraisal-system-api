namespace Document.Documents.Features.UpdateDocument;

internal class UpdateDocumentHandler(IDocumentRepository documentRepository) 
    : ICommandHandler<UpdateDocumentCommand, UpdateDocumentResult>
{
    public async Task<UpdateDocumentResult> Handle(UpdateDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = await documentRepository.GetDocumentById(command.Id, false, cancellationToken);

        document.UpdateComment(command.NewComment);

        await documentRepository.SaveChangesAsync(cancellationToken);

        return new UpdateDocumentResult(true);
    }
}