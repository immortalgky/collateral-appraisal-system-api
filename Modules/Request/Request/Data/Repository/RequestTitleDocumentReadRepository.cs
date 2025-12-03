namespace Request.Data.Repository;

public class RequestTitleDocumentReadRepository(RequestDbContext dbContext) : IRequestTitleDocumentReadRepository
{
    public async Task<List<RequestTitleDocument>> GetRequestTitleDocumentsByTitleIdAsync(Guid titleId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RequestTitleDocuments.Where(rtd  => rtd.TitleId == titleId).ToListAsync();
    }

    public async Task<RequestTitleDocument> GetRequestTitleDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.RequestTitleDocuments.FindAsync([id], cancellationToken);
    }
}