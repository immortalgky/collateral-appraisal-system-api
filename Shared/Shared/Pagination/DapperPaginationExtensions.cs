using Dapper;
using Shared.Data;

namespace Shared.Pagination;

/// <summary>
/// Extension methods for Dapper pagination with raw SQL.
/// </summary>
public static class DapperPaginationExtensions
{
    /// <summary>
    /// Executes a paginated query and returns a PaginatedResult.
    /// </summary>
    /// <param name="connectionFactory">The SQL connection factory.</param>
    /// <param name="sql">The base SQL query (without ORDER BY, OFFSET, FETCH).</param>
    /// <param name="orderBy">The ORDER BY clause (e.g., "CreatedOn DESC").</param>
    /// <param name="request">The pagination request.</param>
    /// <param name="param">Optional query parameters.</param>
    /// <returns>A paginated result.</returns>
    public static async Task<PaginatedResult<T>> QueryPaginatedAsync<T>(
        this ISqlConnectionFactory connectionFactory,
        string sql,
        string orderBy,
        PaginationRequest request,
        object? param = null)
    {
        var connection = connectionFactory.GetOpenConnection();

        // Count query
        var countSql = $"SELECT COUNT(*) FROM ({sql}) AS CountQuery";
        var count = await connection.ExecuteScalarAsync<int>(countSql, param);

        // Data query with pagination
        var offset = request.PageNumber * request.PageSize;
        var pagedSql = $@"{sql}
            ORDER BY {orderBy}
            OFFSET {offset} ROWS FETCH NEXT {request.PageSize} ROWS ONLY";

        var items = await connection.QueryAsync<T>(pagedSql, param);

        return new PaginatedResult<T>(items.ToList(), count, request.PageNumber, request.PageSize);
    }

    /// <summary>
    /// Executes a query and returns results without pagination.
    /// </summary>
    /// <param name="connectionFactory">The SQL connection factory.</param>
    /// <param name="sql">The SQL query.</param>
    /// <param name="param">Optional query parameters.</param>
    /// <returns>A list of results.</returns>
    public static async Task<IEnumerable<T>> QueryAsync<T>(
        this ISqlConnectionFactory connectionFactory,
        string sql,
        object? param = null)
    {
        var connection = connectionFactory.GetOpenConnection();
        return await connection.QueryAsync<T>(sql, param);
    }

    /// <summary>
    /// Executes a query and returns a single result or default.
    /// </summary>
    /// <param name="connectionFactory">The SQL connection factory.</param>
    /// <param name="sql">The SQL query.</param>
    /// <param name="param">Optional query parameters.</param>
    /// <returns>A single result or default.</returns>
    public static async Task<T?> QueryFirstOrDefaultAsync<T>(
        this ISqlConnectionFactory connectionFactory,
        string sql,
        object? param = null)
    {
        var connection = connectionFactory.GetOpenConnection();
        return await connection.QueryFirstOrDefaultAsync<T>(sql, param);
    }

    /// <summary>
    /// Executes a scalar query and returns the result.
    /// </summary>
    /// <param name="connectionFactory">The SQL connection factory.</param>
    /// <param name="sql">The SQL query.</param>
    /// <param name="param">Optional query parameters.</param>
    /// <returns>The scalar result.</returns>
    public static async Task<T?> ExecuteScalarAsync<T>(
        this ISqlConnectionFactory connectionFactory,
        string sql,
        object? param = null)
    {
        var connection = connectionFactory.GetOpenConnection();
        return await connection.ExecuteScalarAsync<T>(sql, param);
    }

    /// <summary>
    /// Appends ORDER BY and pagination clauses to a SQL string.
    /// </summary>
    public static string WithPagination(this string sql, string orderBy, PaginationRequest request)
    {
        var offset = request.PageNumber * request.PageSize;
        return $@"{sql}
            ORDER BY {orderBy}
            OFFSET {offset} ROWS FETCH NEXT {request.PageSize} ROWS ONLY";
    }

    /// <summary>
    /// Wraps SQL in a COUNT query.
    /// </summary>
    public static string ToCountSql(this string sql)
    {
        return $"SELECT COUNT(*) FROM ({sql}) AS CountQuery";
    }
}
