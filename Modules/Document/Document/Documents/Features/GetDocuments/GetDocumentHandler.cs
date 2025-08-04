namespace Document.Documents.Features.GetDocuments;

internal class GetDocumentHandler(IDocumentRepository documentRepository)
    : IQueryHandler<GetDocumentQuery, GetDocumentResult>
{
    public async Task<GetDocumentResult> Handle(GetDocumentQuery query, CancellationToken cancellationToken)
    {
        var documents = await documentRepository.GetDocuments(cancellationToken);

        var result = documents.Adapt<List<DocumentDto>>();

        return new GetDocumentResult(result);
    }
}