using Document.Documents.Features.UploadDocument;

namespace Document.Services;

public interface IDocumentService
{
    Task<UploadDocumentResult> UploadAsync(IReadOnlyList<IFormFile> files, string relateRequest, long relateId, CancellationToken cancellationToken = default);

    Task<(Documents.Models.Document, UploadResultDto)> ProcessFileAsync(IFormFile file, string request, long id, CancellationToken cancellationToken = default);

    Task<bool> DeleteFileAsync(long id, CancellationToken cancellationToken = default);

}