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

    public async Task<bool> DeleteDocument(long id,
        CancellationToken cancellationToken = default)
    {
        var document = await GetDocumentById(id, false, cancellationToken);

        dbContext.Documents.Remove(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}