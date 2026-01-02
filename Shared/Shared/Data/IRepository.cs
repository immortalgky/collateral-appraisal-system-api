using System.Linq.Expressions;
using Shared.DDD;

namespace Shared.Data;

/// <summary>
/// Interface for write repository operations.
/// For reads, use Dapper with ISqlConnectionFactory directly in query handlers.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TId">The entity ID type.</typeparam>
public interface IRepository<T, TId> where T : IEntity<TId>
{
    // Read for updates (with tracking)
    Task<T?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    // Write operations
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(TId id, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    // Unit of Work
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}