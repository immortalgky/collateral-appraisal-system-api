using Shared.CQRS;

namespace Document.Domain.Documents.Features.GetDocumentById;

internal class GetDocumentByIdHandler(IDocumentRepository documentRepository)
    : IQueryHandler<GetDocumentByIdQuery, GetDocumentByIdResult>
{
    public async Task<GetDocumentByIdResult> Handle(GetDocumentByIdQuery query, CancellationToken cancellationToken)
    {
        var document = await documentRepository.GetByIdAsync(query.Id, cancellationToken);

        if (document is null || document.IsDeleted)
            throw new NotFoundException("Document", query.Id);

        var result = document.Adapt<DocumentDto>();

        return new GetDocumentByIdResult(result);
    }
}