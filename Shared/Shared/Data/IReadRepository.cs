using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Shared.DDD;
using Shared.Pagination;

namespace Shared.Data
{
    /// <summary>
    /// Interface for read-only repository operations.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TId">The entity ID type.</typeparam>
    public interface IReadRepository<T, TId> where T : IEntity<TId>
    {
        // Basic read operations
        Task<T?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
        Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);

        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<T>> FindAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

        // Existence checks
        Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

        // Count operations
        Task<int> CountAsync(CancellationToken cancellationToken = default);
        Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
        Task<int> CountAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

        // Paginated operations
        Task<PaginatedResult<T>> GetPaginatedAsync(PaginationRequest request,
            CancellationToken cancellationToken = default);

        Task<PaginatedResult<T>> GetPaginatedAsync(PaginationRequest request, Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<PaginatedResult<T>> GetPaginatedAsync(PaginationRequest request, ISpecification<T> specification,
            CancellationToken cancellationToken = default);

        // Sorted paginated operations
        Task<PaginatedResult<T>> GetPaginatedAsync<TKey>(PaginationRequest request, Expression<Func<T, TKey>> orderBy,
            bool ascending = true, CancellationToken cancellationToken = default);

        Task<PaginatedResult<T>> GetPaginatedAsync<TKey>(PaginationRequest request, Expression<Func<T, bool>> predicate,
            Expression<Func<T, TKey>> orderBy, bool ascending = true, CancellationToken cancellationToken = default);

        Task<PaginatedResult<T>> GetPaginatedAsync<TKey>(PaginationRequest request, ISpecification<T> specification,
            Expression<Func<T, TKey>> orderBy, bool ascending = true, CancellationToken cancellationToken = default);

        // Projection operations
        Task<PaginatedResult<TProjection>> GetPaginatedAsync<TProjection>(PaginationRequest request,
            Expression<Func<T, TProjection>> selector, CancellationToken cancellationToken = default);

        Task<PaginatedResult<TProjection>> GetPaginatedAsync<TProjection>(PaginationRequest request,
            Expression<Func<T, bool>> predicate, Expression<Func<T, TProjection>> selector,
            CancellationToken cancellationToken = default);

        Task<PaginatedResult<TProjection>> GetPaginatedAsync<TProjection>(PaginationRequest request,
            ISpecification<T> specification, Expression<Func<T, TProjection>> selector,
            CancellationToken cancellationToken = default);

        // Sorted projection operations
        Task<PaginatedResult<TProjection>> GetPaginatedAsync<TKey, TProjection>(PaginationRequest request,
            Expression<Func<T, TKey>> orderBy, Expression<Func<T, TProjection>> selector, bool ascending = true,
            CancellationToken cancellationToken = default);

        Task<PaginatedResult<TProjection>> GetPaginatedAsync<TKey, TProjection>(PaginationRequest request,
            Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderBy,
            Expression<Func<T, TProjection>> selector, bool ascending = true,
            CancellationToken cancellationToken = default);

        Task<PaginatedResult<TProjection>> GetPaginatedAsync<TKey, TProjection>(PaginationRequest request,
            ISpecification<T> specification, Expression<Func<T, TKey>> orderBy,
            Expression<Func<T, TProjection>> selector, bool ascending = true,
            CancellationToken cancellationToken = default);
    }
}