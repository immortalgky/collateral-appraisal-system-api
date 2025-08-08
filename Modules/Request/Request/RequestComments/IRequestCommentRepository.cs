namespace Request.RequestComments;

public interface IRequestCommentRepository
{
    Task<RequestComment> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddAsync(RequestComment requestComment, CancellationToken cancellationToken = default);
    void Remove(RequestComment requestComment, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}