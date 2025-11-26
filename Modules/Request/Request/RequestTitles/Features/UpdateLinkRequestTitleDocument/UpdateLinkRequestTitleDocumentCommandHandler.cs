namespace Request.RequestTitles.Features.UpdateLinkRequestTitleDocument;

public class UpdateLinkRequestTitleDocumentCommandHandler(IRequestTitleDocumentRepository requestTitleDocumentRepository) : ICommandHandler<UpdateLinkRequestTitleDocumentCommand, UpdateLinkRequestTitleDocumentResult>
{
  public async Task<UpdateLinkRequestTitleDocumentResult> Handle(UpdateLinkRequestTitleDocumentCommand command, CancellationToken cancellationToken)
  {
    if (command.RequestTitleDocDto.Id is null)
      throw new Exception("no request title document id is given");
    
    var requestTitleDoc = await requestTitleDocumentRepository.GetRequestTitleDocumentByIdAsync(command.RequestTitleDocDto.Id!.Value, cancellationToken);
    
    if (requestTitleDoc is null)
      throw new RequestTitleDocumentNotFoundException(command.RequestTitleDocDto.Id!.Value);
    
    requestTitleDoc.Update(new RequestTitleDocumentData
    {
      DocumentId = command.RequestTitleDocDto.DocumentId,
      DocumentType = command.RequestTitleDocDto.DocumentType,
      Filename = command.RequestTitleDocDto.Filename,
      Prefix = command.RequestTitleDocDto.Prefix,
      Set = command.RequestTitleDocDto.Set,
      DocumentDescription = command.RequestTitleDocDto.DocumentDescription,
      FilePath = command.RequestTitleDocDto.FilePath,
      CreatedWorkstation = command.RequestTitleDocDto.CreatedWorkstation,
      IsRequired = command.RequestTitleDocDto.IsRequired,
      UploadedBy = command.RequestTitleDocDto.UploadedBy,
      UploadedByName = command.RequestTitleDocDto.UploadedByName
    });
    
    await requestTitleDocumentRepository.SaveChangeAsync(cancellationToken);
    
    return new UpdateLinkRequestTitleDocumentResult(true);
  }
}