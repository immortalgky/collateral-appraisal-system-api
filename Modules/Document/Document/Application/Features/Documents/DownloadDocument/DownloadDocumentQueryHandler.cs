using Shared.CQRS;

namespace Document.Domain.Documents.Features.DownloadDocument;

internal class DownloadDocumentQueryHandler(IDocumentRepository documentRepository)
    : IQueryHandler<DownloadDocumentQuery, DownloadDocumentResult>
{
    public async Task<DownloadDocumentResult> Handle(
        DownloadDocumentQuery query,
        CancellationToken cancellationToken)
    {
        var document = await documentRepository.GetByIdAsync(query.Id, cancellationToken);

        if (document is null || document.IsDeleted || !document.IsActive)
        {
            return new DownloadDocumentResult(
                FilePath: string.Empty,
                MimeType: string.Empty,
                FileName: string.Empty,
                FileExists: false
            );
        }

        var fileExists = File.Exists(document.StoragePath);

        return new DownloadDocumentResult(
            FilePath: document.StoragePath,
            MimeType: document.MimeType ?? "application/octet-stream",
            FileName: document.FileName,
            FileExists: fileExists
        );
    }
}
