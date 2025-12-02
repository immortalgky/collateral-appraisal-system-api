namespace Request.RequestComments;

public interface IRequestCommentRepository
{
    Task<RequestComment> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<RequestComment>> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task AddAsync(RequestComment requestComment, CancellationToken cancellationToken = default);
    void Remove(RequestComment requestComment, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}