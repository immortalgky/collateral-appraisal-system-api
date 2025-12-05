using System.Linq.Expressions;
using Shared.DDD;

namespace Shared.Data;

/// <summary>
/// Interface for repository operations.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TId">The entity ID type.</typeparam>
public interface IRepository<T, TId> : IReadRepository<T, TId> where T : IEntity<TId>
{
    // Read for updates
    Task<T?> GetByIdForUpdateAsync(TId id, CancellationToken cancellationToken = default);

    Task<IEnumerable<T>> FindForUpdateAsync(Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<T>> FindForUpdateAsync(ISpecification<T> specification,
        CancellationToken cancellationToken = default);

    Task<T?> FirstOrDefaultForUpdateAsync(Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<T?> FirstOrDefaultForUpdateAsync(ISpecification<T> specification,
        CancellationToken cancellationToken = default);

    // Write operations
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(TId id, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
}