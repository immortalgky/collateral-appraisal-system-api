using Microsoft.EntityFrameworkCore.Storage;

namespace Appraisal.Infrastructure;

/// <summary>
/// Unit of Work implementation for the Appraisal module.
/// Coordinates transactions across repositories.
/// </summary>
public class AppraisalUnitOfWork : IAppraisalUnitOfWork
{
    private readonly AppraisalDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    public AppraisalUnitOfWork(AppraisalDbContext context)
    {
        _context = context;
    }

    public bool HasActiveTransaction => _currentTransaction is not null;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
            throw new InvalidOperationException("A transaction is already in progress");

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return _currentTransaction;
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
            throw new InvalidOperationException("No transaction in progress");

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
            return;

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public IRepository<T, TId> Repository<T, TId>() where T : class, IEntity<TId>
    {
        // For now, throw - repositories should be injected directly
        throw new NotImplementedException(
            "Use dependency injection to resolve repositories. " +
            "Inject IAppraisalRepository, IAppraisalCollateralRepository, etc. directly.");
    }

    private async Task DisposeTransactionAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}