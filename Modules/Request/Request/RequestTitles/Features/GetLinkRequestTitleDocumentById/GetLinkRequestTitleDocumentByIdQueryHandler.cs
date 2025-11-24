namespace Request.RequestTitles.Features.GetLinkRequestTitleDocumentById;

public class GetLinkRequestTitleDocumentByIdQueryHandler(IRequestTitleDocumentReadRepository requestTitleDocumentReadRepository) : IQueryHandler<GetLinkRequestTitleDocumentByIdQuery, GetLinkRequestTitleDocumentByIdResult>
{
  public async Task<GetLinkRequestTitleDocumentByIdResult> Handle(GetLinkRequestTitleDocumentByIdQuery request, CancellationToken cancellationToken)
  {
    var requestTitleDocument = await requestTitleDocumentReadRepository.GetRequestTitleDocumentByIdAsync(request.TitleDocId, cancellationToken);
    
    if (requestTitleDocument is null)
      throw new RequestTitleDocumentNotFoundException(request.TitleDocId);

    var result = new GetLinkRequestTitleDocumentByIdResult(
      requestTitleDocument.Id,
      requestTitleDocument.TitleId,
      requestTitleDocument.DocumentId,
      requestTitleDocument.DocumentType,
      requestTitleDocument.IsRequired,
      requestTitleDocument.DocumentDescription,
      requestTitleDocument.UploadedBy,
      requestTitleDocument.UploadedByName
    );
    
    return result;
  }
}