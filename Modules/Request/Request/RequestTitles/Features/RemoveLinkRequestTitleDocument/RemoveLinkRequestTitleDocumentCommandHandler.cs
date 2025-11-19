namespace Request.RequestTitles.Features.RemoveLinkRequestTitleDocument;

public class RemoveLinkRequestTitleDocumentCommandHandler(IRequestTitleDocumentRepository requestTitleDocumentRepository) : ICommandHandler<RemoveLinkRequestTitleDocumentCommand, RemoveLinkRequestTitleDocumentResult>
{
    public async Task<RemoveLinkRequestTitleDocumentResult> Handle(RemoveLinkRequestTitleDocumentCommand command, CancellationToken cancellationToken)
    {
        var requestTitleDocument = await requestTitleDocumentRepository.GetRequestTitleDocumentByIdAsync(command.Id);
        
        if (requestTitleDocument is null)
            throw new RequestTitleDocumentNotFoundException(command.Id);
        
        if (requestTitleDocument.TitleId != command.TitleId)
            throw new Exception($"RequestId unmatch {requestTitleDocument.TitleId} : {command.TitleId}");

        await requestTitleDocumentRepository.Remove(requestTitleDocument);
        
        await requestTitleDocumentRepository.SaveChangeAsync(cancellationToken);
        
        return new RemoveLinkRequestTitleDocumentResult(true);
    }
}