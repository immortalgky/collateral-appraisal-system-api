using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Shared.DDD;

namespace Shared.Data;

/// <summary>
/// Base implementation of a repository.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TId">The entity ID type.</typeparam>
public abstract class BaseRepository<T, TId> : BaseReadRepository<T, TId>, IRepository<T, TId>
    where T : class, IEntity<TId>
{
    protected BaseRepository(DbContext context) : base(context)
    {
    }

    // Write operations
    public virtual async Task<T?> GetByIdForUpdateAsync(TId id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync([id], cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> FindForUpdateAsync(Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> FindForUpdateAsync(ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(specification.ToExpression()).ToListAsync(cancellationToken);
    }

    public virtual async Task<T?> FirstOrDefaultForUpdateAsync(Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public virtual async Task<T?> FirstOrDefaultForUpdateAsync(ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(specification.ToExpression(), cancellationToken);
    }

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
    }

    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await DbSet.AddRangeAsync(entities, cancellationToken);
    }

    public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        DbSet.UpdateRange(entities);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(TId id, CancellationToken cancellationToken = default)
    {
        var entity = await DbSet.FindAsync([id], cancellationToken);
        if (entity != null) DbSet.Remove(entity);
    }

    public virtual Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        DbSet.RemoveRange(entities);
        return Task.CompletedTask;
    }
}