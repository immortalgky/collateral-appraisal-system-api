using Microsoft.EntityFrameworkCore;

namespace Shared.Pagination
{
    /// <summary>
    /// Extension methods for working with paginated queries.
    /// </summary>
    public static class PaginationExtensions
    {
        /// <summary>
        /// Converts a queryable to a paginated result.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="query">The queryable to paginate.</param>
        /// <param name="request">The pagination request.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A paginated result.</returns>
        public static async Task<PaginatedResult<T>> ToPaginatedResultAsync<T>(
            this IQueryable<T> query,
            PaginationRequest request,
            CancellationToken cancellationToken = default)
        {
            var count = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip(request.PageNumber * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            return new PaginatedResult<T>(items, count, request.PageNumber, request.PageSize);
        }

        /// <summary>
        /// Applies pagination to a queryable.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="query">The queryable to paginate.</param>
        /// <param name="request">The pagination request.</param>
        /// <returns>A queryable with pagination applied.</returns>
        public static IQueryable<T> Paginate<T>(this IQueryable<T> query, PaginationRequest request)
        {
            return query
                .Skip(request.PageNumber * request.PageSize)
                .Take(request.PageSize);
        }
    }
}