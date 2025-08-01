using Document.Services;

namespace Document.Documents.Features.UploadDocument;

internal class UploadDocumentCommandHandler(IDocumentService documentService) 
    : ICommandHandler<UploadDocumentCommand, UploadDocumentResult>
{
    public async Task<UploadDocumentResult> Handle(UploadDocumentCommand command, CancellationToken cancellationToken)
    {
        var result = await documentService.UploadAsync(command.Documents, command.RelateRequest, command.RelateId, cancellationToken);

        return result;
    }
}