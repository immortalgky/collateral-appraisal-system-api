namespace Document.Documents.Features.GetDocumentById;

internal class GetDocumentByIdHandler(IDocumentRepository documentRepository) : IQueryHandler<GetDocumentByIdQuery, GetDocumentByIdResult>
{
    public async Task<GetDocumentByIdResult> Handle(GetDocumentByIdQuery query, CancellationToken cancellationToken)
    {
        var document = await documentRepository.GetDocumentById(query.Id, false, cancellationToken);

        var result = document.Adapt<DocumentDto>();

        return new GetDocumentByIdResult(result);
    }
}