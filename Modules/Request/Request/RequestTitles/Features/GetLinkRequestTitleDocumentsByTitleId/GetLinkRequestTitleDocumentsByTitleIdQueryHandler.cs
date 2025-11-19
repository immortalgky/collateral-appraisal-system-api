namespace Request.RequestTitles.Features.GetLinkRequestTitleDocumentsByTitleId;

public class GetLinkRequestTitleDocumentsByTitleIdQueryHandler(IRequestTitleDocumentReadRepository requestTitleDocumentReadRepository) : IQueryHandler<GetLinkRequestTitleDocumentsByTitleIdQuery, GetLinkRequestTitleDocumentsByTitleIdResult>
{
    public async Task<GetLinkRequestTitleDocumentsByTitleIdResult> Handle(GetLinkRequestTitleDocumentsByTitleIdQuery query, CancellationToken cancellationToken)
    {
        var requestTitleDocuments = await requestTitleDocumentReadRepository.GetRequestTitleDocumentsByTitleIdAsync(query.TitleId, cancellationToken);
        
        var result = new GetLinkRequestTitleDocumentsByTitleIdResult(requestTitleDocuments.Select(rtd => rtd.Adapt<RequestTitleDocumentDto>()).ToList());

        return result;
    }
}