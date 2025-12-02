using Request.RequestComments;
using Request.RequestComments.Models;

namespace Request.Data.Repository;

public class RequestCommentRepository(RequestDbContext dbContext) : IRequestCommentRepository
{
    public async Task<RequestComment> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.RequestComments.FindAsync(id);
    }

    public async Task AddAsync(RequestComment requestComment, CancellationToken cancellationToken = default)
    {
        await dbContext.RequestComments.AddAsync(requestComment, cancellationToken);
    }

    public void Remove(RequestComment requestComment, CancellationToken cancellationToken = default)
    {
        dbContext.Remove(requestComment);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<RequestComment>> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var comments = await dbContext.RequestComments
           .Where(x => x.RequestId == requestId)
           .ToListAsync(cancellationToken);
        return comments;
    }
}