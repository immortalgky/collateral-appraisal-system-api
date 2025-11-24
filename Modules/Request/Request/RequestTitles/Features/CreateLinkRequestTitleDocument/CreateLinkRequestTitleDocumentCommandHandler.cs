using Request.RequestTitles.Features;

namespace Request.RequestTitles.Features.CreateLinkRequestTitleDocument;

public class CreateLinkRequestTitleDocumentCommandHandler(IRequestTitleRepository requestTitleRepository) : ICommandHandler<CreateLinkRequestTitleDocumentCommand, CreateLinkRequestTitleDocumentResult>
{
    public async Task<CreateLinkRequestTitleDocumentResult> Handle(CreateLinkRequestTitleDocumentCommand command, CancellationToken cancellationToken)
    {
        var requestTitle = await requestTitleRepository.GetByIdAsync(command.TitleId, cancellationToken);
        
        if (requestTitle == null)
            throw new RequestTitleNotFoundException(command.TitleId);
        
        var requestTitleDocument = requestTitle.CreateLinkRequestTitleDocument(command.RequestTitleDto.Adapt<RequestTitleDocumentData>() with {TitleId = command.TitleId});
        
        await requestTitleRepository.SaveChangesAsync();
        
        return new CreateLinkRequestTitleDocumentResult(true);
    }
}