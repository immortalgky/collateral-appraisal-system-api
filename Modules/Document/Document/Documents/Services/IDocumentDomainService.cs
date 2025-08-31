namespace Document.Documents.Services;

public interface IDocumentDomainService
{
    Task<Models.Document> CreateDocumentAsync(
        string relateRequest,
        long relateId,
        string docType,
        string filename,
        string prefix,
        short set,
        string comment,
        Stream fileStream,
        CancellationToken cancellationToken = default);

    Task ValidateDocumentSecurityAsync(Stream fileStream, string filename, CancellationToken cancellationToken = default);
}