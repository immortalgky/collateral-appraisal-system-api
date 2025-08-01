namespace Document.Data.Repository;

public class DocumentRepository(DocumentDbContext dbContext) : IDocumentRepository
{
    public async Task<List<Documents.Models.Document>> GetDocuments (CancellationToken cancellationToken = default)
    {
        var documents = await dbContext.Documents.ToListAsync(cancellationToken);

        return documents;
    }
    public async Task<bool> UploadDocument(Documents.Models.Document document,
        CancellationToken cancellationToken = default)
    {
        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<Documents.Models.Document> GetDocumentById(long documentId, bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Documents
            .Where(r => r.Id == documentId);

        if (asNoTracking) query = query.AsNoTracking();
        var document = await query.SingleOrDefaultAsync(cancellationToken);

        return document ?? throw new DocumentNotFoundException(documentId);
    }

    public async Task<bool> GetDocument(string filePath, string request, long id,
        bool asNoTracking = true, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Documents.Where(r => r.FilePath == filePath);

        var result = await query.AnyAsync(r => r.RelateId == id && r.RelateRequest == request, cancellationToken);
        
        return result; 
    }

    public async Task<bool> DeleteDocument(long id,
        CancellationToken cancellationToken = default)
    {
        var document = await GetDocumentById(id, false, cancellationToken) ?? throw new DocumentNotFoundException(id);

        dbContext.Documents.Remove(document);

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteDocument(string filePath,
        CancellationToken cancellationToken = default)
    {
        var result = await dbContext.Documents.AnyAsync(r => r.FilePath == filePath, cancellationToken);

        return result;
    }
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}