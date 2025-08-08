using Request.RequestComments.Models;

namespace Request.Requests;

public interface IRequestRepository
{
    Task<Requests.Models.Request> GetByIdAsync(long requestId, CancellationToken cancellationToken = default);

    Task<Requests.Models.Request> CreateRequestAsync(Requests.Models.Request request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteRequestAsync(long requestId, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}