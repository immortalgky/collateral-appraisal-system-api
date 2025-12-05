using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Shared.DDD;

namespace Shared.Data;

/// <summary>
/// Implementation of the Unit of Work pattern for managing transactions and coordinating repositories.
/// </summary>
public class UnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
{
    private readonly TContext _context;
    private IDbContextTransaction? _transaction;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<Type, object> _repositories = new();
    private readonly Dictionary<Type, object> _readRepositories = new();
    public bool HasActiveTransaction => _transaction != null;

    /// <summary>
    /// Creates a new instance of UnitOfWork.
    /// </summary>
    /// <param name="context">The DbContext to use.</param>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    public UnitOfWork(TContext context, IServiceProvider serviceProvider)
    {
        _context = context;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Saves all changes made in the context to the database.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Begins a transaction on the database.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null) throw new InvalidOperationException("Transaction is already started");

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        return _transaction;
    }

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null) throw new InvalidOperationException("No transaction to commit");

        try
        {
            await _transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null) throw new InvalidOperationException("No transaction to rollback");

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <summary>
    /// Gets a repository for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TId">The entity ID type.</typeparam>
    /// <returns>A repository for the entity type.</returns>
    public IRepository<T, TId> Repository<T, TId>() where T : class, IEntity<TId>
    {
        var type = typeof(IRepository<T, TId>);

        if (!_repositories.ContainsKey(type))
            _repositories[type] = _serviceProvider.GetRequiredService<IRepository<T, TId>>();

        return (IRepository<T, TId>)_repositories[type];
    }

    /// <summary>
    /// Gets a read-only repository for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TId">The entity ID type.</typeparam>
    /// <returns>A read-only repository for the entity type.</returns>
    public IReadRepository<T, TId> ReadRepository<T, TId>() where T : class, IEntity<TId>
    {
        var type = typeof(IReadRepository<T, TId>);

        if (!_readRepositories.ContainsKey(type))
            _readRepositories[type] = _serviceProvider.GetRequiredService<IReadRepository<T, TId>>();

        return (IReadRepository<T, TId>)_readRepositories[type];
    }

    /// <summary>
    /// Disposes the transaction and context.
    /// </summary>
    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}