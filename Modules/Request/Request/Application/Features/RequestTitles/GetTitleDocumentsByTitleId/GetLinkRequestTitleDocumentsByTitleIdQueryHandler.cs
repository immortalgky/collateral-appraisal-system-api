namespace Request.Application.Features.RequestTitles.GetTitleDocumentsByTitleId;

public class GetLinkRequestTitleDocumentsByTitleIdQueryHandler(IRequestTitleRepository requestTitleRepository)
    : IQueryHandler<GetLinkRequestTitleDocumentsByTitleIdQuery, GetLinkRequestTitleDocumentsByTitleIdResult>
{
    public async Task<GetLinkRequestTitleDocumentsByTitleIdResult> Handle(GetLinkRequestTitleDocumentsByTitleIdQuery query, CancellationToken cancellationToken)
    {
        // Load RequestTitle aggregate with documents
        var title = await requestTitleRepository.GetByIdWithDocumentsAsync(query.TitleId, cancellationToken);
        if (title is null)
            throw new RequestTitleNotFoundException(query.TitleId);

        var titleDocuments = title.Documents;

        var result = new GetLinkRequestTitleDocumentsByTitleIdResult(titleDocuments.Select(rtd => rtd.Adapt<RequestTitleDocumentDto>()).ToList());

        return result;
    }
}
