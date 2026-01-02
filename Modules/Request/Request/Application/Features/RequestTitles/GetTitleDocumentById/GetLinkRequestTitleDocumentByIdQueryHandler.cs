namespace Request.Application.Features.RequestTitles.GetTitleDocumentById;

public class GetLinkRequestTitleDocumentByIdQueryHandler(IRequestTitleRepository requestTitleRepository)
    : IQueryHandler<GetLinkRequestTitleDocumentByIdQuery, GetLinkRequestTitleDocumentByIdResult>
{
    public async Task<GetLinkRequestTitleDocumentByIdResult> Handle(GetLinkRequestTitleDocumentByIdQuery query, CancellationToken cancellationToken)
    {
        // Load RequestTitle aggregate with documents
        var title = await requestTitleRepository.GetByIdWithDocumentsAsync(query.TitleId, cancellationToken);
        if (title is null)
            throw new RequestTitleNotFoundException(query.TitleId);

        var titleDocument = title.GetDocument(query.TitleDocId);
        if (titleDocument is null)
            throw new TitleDocumentNotFoundException(query.TitleDocId);

        var result = new GetLinkRequestTitleDocumentByIdResult(
            titleDocument.Id,
            titleDocument.TitleId,
            titleDocument.DocumentId,
            titleDocument.DocumentType,
            titleDocument.Filename,
            titleDocument.Prefix,
            titleDocument.Set,
            titleDocument.DocumentDescription,
            titleDocument.FilePath,
            titleDocument.CreatedWorkstation,
            titleDocument.IsRequired,
            titleDocument.UploadedBy,
            titleDocument.UploadedByName
        );

        return result;
    }
}
