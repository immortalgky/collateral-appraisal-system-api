using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.DDD;
using Shared.Pagination;

namespace Shared.Data
{
    /// <summary>
    /// Base implementation of a read-only repository.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TId">The entity ID type.</typeparam>
    public abstract class BaseReadRepository<T, TId> : IReadRepository<T, TId> where T : class, IEntity<TId>
    {
        protected readonly DbContext Context;
        protected readonly DbSet<T> DbSet;

        protected BaseReadRepository(DbContext context)
        {
            Context = context;
            DbSet = context.Set<T>();
        }

        /// <summary>
        /// Gets the read query for this repository (with no tracking).
        /// This is used for all standard read operations including pagination.
        /// </summary>
        /// <returns>A queryable of the entity type with no tracking.</returns>
        protected virtual IQueryable<T> GetReadQuery()
        {
            return DbSet.AsNoTracking();
        }

        /// <summary>
        /// Gets the tracked query for this repository.
        /// This is used for write operations or where tracking is needed.
        /// </summary>
        /// <returns>A queryable of the entity type with tracking enabled.</returns>
        protected virtual IQueryable<T> GetTrackedQuery()
        {
            return DbSet;
        }

        // Basic read operations
        public virtual async Task<T?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().FirstOrDefaultAsync(e => e.Id.Equals(id), cancellationToken);
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().ToListAsync(cancellationToken);
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().Where(predicate).ToListAsync(cancellationToken);
        }

        public virtual async Task<IEnumerable<T>> FindAsync(ISpecification<T> specification,
            CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().Where(specification.ToExpression()).ToListAsync(cancellationToken);
        }

        public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public virtual async Task<T?> FirstOrDefaultAsync(ISpecification<T> specification,
            CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().FirstOrDefaultAsync(specification.ToExpression(), cancellationToken);
        }

        // Existence checks
        public virtual async Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().AnyAsync(e => e.Id.Equals(id), cancellationToken);
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().AnyAsync(predicate, cancellationToken);
        }

        public virtual async Task<bool> ExistsAsync(ISpecification<T> specification,
            CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().AnyAsync(specification.ToExpression(), cancellationToken);
        }

        // Count operations
        public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().CountAsync(cancellationToken);
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().CountAsync(predicate, cancellationToken);
        }

        public virtual async Task<int> CountAsync(ISpecification<T> specification,
            CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().CountAsync(specification.ToExpression(), cancellationToken);
        }

        // Paginated operations
        public virtual async Task<PaginatedResult<T>> GetPaginatedAsync(PaginationRequest request,
            CancellationToken cancellationToken = default)
        {
            var query = GetReadQuery();
            return await CreatePaginatedResultAsync(query, request, cancellationToken);
        }

        public virtual async Task<PaginatedResult<T>> GetPaginatedAsync(PaginationRequest request,
            Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var query = GetReadQuery().Where(predicate);
            return await CreatePaginatedResultAsync(query, request, cancellationToken);
        }

        public virtual async Task<PaginatedResult<T>> GetPaginatedAsync(PaginationRequest request,
            ISpecification<T> specification, CancellationToken cancellationToken = default)
        {
            var query = GetReadQuery().Where(specification.ToExpression());
            return await CreatePaginatedResultAsync(query, request, cancellationToken);
        }

        // Sorted paginated operations
        public virtual async Task<PaginatedResult<T>> GetPaginatedAsync<TKey>(PaginationRequest request,
            Expression<Func<T, TKey>> orderBy, bool ascending = true, CancellationToken cancellationToken = default)
        {
            var query = GetReadQuery();
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            return await CreatePaginatedResultAsync(query, request, cancellationToken);
        }

        public virtual async Task<PaginatedResult<T>> GetPaginatedAsync<TKey>(PaginationRequest request,
            Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderBy, bool ascending = true,
            CancellationToken cancellationToken = default)
        {
            var query = GetReadQuery().Where(predicate);
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            return await CreatePaginatedResultAsync(query, request, cancellationToken);
        }

        public virtual async Task<PaginatedResult<T>> GetPaginatedAsync<TKey>(PaginationRequest request,
            ISpecification<T> specification, Expression<Func<T, TKey>> orderBy, bool ascending = true,
            CancellationToken cancellationToken = default)
        {
            var query = GetReadQuery().Where(specification.ToExpression());
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            return await CreatePaginatedResultAsync(query, request, cancellationToken);
        }

        // Basic projection operations
        public virtual async Task<TProjection?> GetByIdAsync<TProjection>(TId id,
            Expression<Func<T, TProjection>> selector, CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().Where(e => e.Id.Equals(id)).Select(selector)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public virtual async Task<IEnumerable<TProjection>> GetAllAsync<TProjection>(
            Expression<Func<T, TProjection>> selector, CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().Select(selector).ToListAsync(cancellationToken);
        }

        public virtual async Task<IEnumerable<TProjection>> FindAsync<TProjection>(
            Expression<Func<T, bool>> predicate, Expression<Func<T, TProjection>> selector,
            CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().Where(predicate).Select(selector).ToListAsync(cancellationToken);
        }

        public virtual async Task<IEnumerable<TProjection>> FindAsync<TProjection>(ISpecification<T> specification,
            Expression<Func<T, TProjection>> selector, CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().Where(specification.ToExpression()).Select(selector)
                .ToListAsync(cancellationToken);
        }

        public virtual async Task<TProjection?> FirstOrDefaultAsync<TProjection>(
            Expression<Func<T, bool>> predicate, Expression<Func<T, TProjection>> selector,
            CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().Where(predicate).Select(selector).FirstOrDefaultAsync(cancellationToken);
        }

        public virtual async Task<TProjection?> FirstOrDefaultAsync<TProjection>(ISpecification<T> specification,
            Expression<Func<T, TProjection>> selector, CancellationToken cancellationToken = default)
        {
            return await GetReadQuery().Where(specification.ToExpression()).Select(selector)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Paginated projection operations
        public virtual async Task<PaginatedResult<TProjection>> GetPaginatedAsync<TProjection>(
            PaginationRequest request, Expression<Func<T, TProjection>> selector,
            CancellationToken cancellationToken = default)
        {
            var query = GetReadQuery().Select(selector);
            return await CreatePaginatedResultAsync(query, request, cancellationToken);
        }

        public virtual async Task<PaginatedResult<TProjection>> GetPaginatedAsync<TProjection>(
            PaginationRequest request, Expression<Func<T, bool>> predicate, Expression<Func<T, TProjection>> selector,
            CancellationToken cancellationToken = default)
        {
            var query = GetReadQuery().Where(predicate).Select(selector);
            return await CreatePaginatedResultAsync(query, request, cancellationToken);
        }

        public virtual async Task<PaginatedResult<TProjection>> GetPaginatedAsync<TProjection>(
            PaginationRequest request, ISpecification<T> specification, Expression<Func<T, TProjection>> selector,
            CancellationToken cancellationToken = default)
        {
            var query = GetReadQuery().Where(specification.ToExpression()).Select(selector);
            return await CreatePaginatedResultAsync(query, request, cancellationToken);
        }

        // Sorted projection operations
        public virtual async Task<PaginatedResult<TProjection>> GetPaginatedAsync<TKey, TProjection>(
            PaginationRequest request, Expression<Func<T, TKey>> orderBy, Expression<Func<T, TProjection>> selector,
            bool ascending = true, CancellationToken cancellationToken = default)
        {
            var query = GetReadQuery();
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            var projectedQuery = query.Select(selector);
            return await CreatePaginatedResultAsync(projectedQuery, request, cancellationToken);
        }

        public virtual async Task<PaginatedResult<TProjection>> GetPaginatedAsync<TKey, TProjection>(
            PaginationRequest request, Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderBy,
            Expression<Func<T, TProjection>> selector, bool ascending = true,
            CancellationToken cancellationToken = default)
        {
            var query = GetReadQuery().Where(predicate);
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            var projectedQuery = query.Select(selector);
            return await CreatePaginatedResultAsync(projectedQuery, request, cancellationToken);
        }

        public virtual async Task<PaginatedResult<TProjection>> GetPaginatedAsync<TKey, TProjection>(
            PaginationRequest request, ISpecification<T> specification, Expression<Func<T, TKey>> orderBy,
            Expression<Func<T, TProjection>> selector, bool ascending = true,
            CancellationToken cancellationToken = default)
        {
            var query = GetReadQuery().Where(specification.ToExpression());
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            var projectedQuery = query.Select(selector);
            return await CreatePaginatedResultAsync(projectedQuery, request, cancellationToken);
        }

        // Helper method to create paginated results
        private async Task<PaginatedResult<TItem>> CreatePaginatedResultAsync<TItem>(IQueryable<TItem> query,
            PaginationRequest request, CancellationToken cancellationToken)
        {
            var count = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip(request.PageNumber * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            return new PaginatedResult<TItem>(items, count, request.PageNumber, request.PageSize);
        }
    }
}