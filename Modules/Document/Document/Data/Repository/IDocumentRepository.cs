namespace Document.Data.Repository;

public interface IDocumentRepository
{
    Task<List<Documents.Models.Document>> GetDocuments(CancellationToken cancellationToken = default);
    Task<bool> UploadDocument(Documents.Models.Document document,
        CancellationToken cancellationToken = default);

    Task<Documents.Models.Document> GetDocumentById(long documentId, bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    Task<bool> GetDocument(string filePath, string request, long id,
        bool asNoTracking = true, CancellationToken cancellationToken = default);

    Task<bool> DeleteDocument(long id,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteDocument(string filePath,
       CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}