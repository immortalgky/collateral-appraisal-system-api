using Document.Domain.Documents;
using Shared.CQRS;

namespace Integration.Application.Features.Documents.GetDocument;

public class GetDocumentQueryHandler(
    IDocumentRepository documentRepository
) : IQueryHandler<GetDocumentQuery, GetDocumentResult>
{
    public async Task<GetDocumentResult> Handle(
        GetDocumentQuery query,
        CancellationToken cancellationToken)
    {
        var document = await documentRepository.GetByIdAsync(query.DocumentId, cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException($"Document {query.DocumentId} not found");
        }

        var downloadUrl = $"/documents/{document.Id}/download";

        return new GetDocumentResult(
            document.Id,
            document.FileName,
            document.MimeType,
            document.FileSizeBytes,
            document.DocumentType,
            document.DocumentCategory,
            downloadUrl,
            document.UploadedAt
        );
    }
}
