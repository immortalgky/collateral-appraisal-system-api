using System.Globalization;

namespace Request.Data.Repository;

public class RequestRepository(RequestDbContext dbContext) : IRequestRepository
{
    public async Task<Requests.Models.Request> GetRequest(long requestId, bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Requests
            .Where(r => r.Id == requestId);

        if (asNoTracking) query = query.AsNoTracking();

        var request = await query.SingleOrDefaultAsync(cancellationToken);

        return request ?? throw new RequestNotFoundException(requestId);
    }

    public async Task<Requests.Models.Request> CreateRequest(Requests.Models.Request request,
        CancellationToken cancellationToken = default)
    {
        dbContext.Requests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);

        return request;
    }

    public async Task<bool> DeleteRequest(long requestId, CancellationToken cancellationToken = default)
    {
        var request = await GetRequest(requestId, false, cancellationToken);

        dbContext.Requests.Remove(request);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<int> GetNextAppraisalNumber(CancellationToken cancellationToken = default)
    {
        return (await dbContext.Database
            .SqlQuery<int>($"EXEC request.GetNextAppraisalNumber").ToListAsync(cancellationToken)).FirstOrDefault();
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}