using Microsoft.EntityFrameworkCore.Storage;
using Shared.DDD;

namespace Shared.Data;

/// <summary>
/// Interface for the Unit of Work pattern to manage transactions and coordination of multiple repositories.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    public bool HasActiveTransaction { get; }

    /// <summary>
    /// Saves all changes made in the context to the database.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a transaction on the database.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a repository for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TId">The entity ID type.</typeparam>
    /// <returns>A repository for the entity type.</returns>
    IRepository<T, TId> Repository<T, TId>() where T : class, IEntity<TId>;

}