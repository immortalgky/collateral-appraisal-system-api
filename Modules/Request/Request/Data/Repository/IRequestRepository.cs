namespace Request.Data.Repository;

public interface IRequestRepository
{
    Task<Requests.Models.Request> GetRequest(long requestId, bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    Task<Requests.Models.Request> CreateRequest(Requests.Models.Request request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteRequest(long requestId, CancellationToken cancellationToken = default);
    Task<int> GetNextAppraisalNumber(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}