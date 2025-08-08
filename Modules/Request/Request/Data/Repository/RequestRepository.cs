using Request.Services;

namespace Request.Data.Repository;

public class RequestRepository(RequestDbContext dbContext, IAppraisalNumberGenerator generator) : IRequestRepository
{
    public async Task<Requests.Models.Request> GetByIdAsync(long requestId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Requests.FindAsync([requestId], cancellationToken);
    }

    public async Task<Requests.Models.Request> CreateRequestAsync(Requests.Models.Request request,
        CancellationToken cancellationToken = default)
    {
        // Generate appraisal number if not already set
        if (request.AppraisalNo == null)
        {
            var appraisalNumber = await generator.GenerateAsync(cancellationToken);
            request.SetAppraisalNumber(appraisalNumber);
        }

        dbContext.Requests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);

        return request;
    }

    public async Task<bool> DeleteRequestAsync(long requestId, CancellationToken cancellationToken = default)
    {
        var request = await GetByIdAsync(requestId, cancellationToken);

        dbContext.Requests.Remove(request);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}