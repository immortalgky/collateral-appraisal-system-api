using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using Shared.Data;
using Shared.Exceptions;

namespace Shared.Pagination;

/// <summary>
/// Extension methods for Dapper pagination with raw SQL.
/// </summary>
public static class DapperPaginationExtensions
{
    // Defense-in-depth guard. orderBy is never user-supplied here — callers pass server-built
    // constants or columns already whitelisted upstream — but this blocks the classic ORDER BY
    // injection vectors if a future caller forgets. We deny statement terminators (;), string
    // literals (', "), and comment markers (--, /* */) rather than allow-listing characters, so
    // legitimate function expressions (COALESCE(...), STDistance(geography::Point(...)), CAST(...))
    // used by e.g. History Search pass through.
    private static readonly Regex _orderByInjectionPattern =
        new(@"(;|'|""|--|/\*|\*/)",
            RegexOptions.Compiled,
            TimeSpan.FromSeconds(1));

    private static void ValidateOrderBy(string orderBy)
    {
        // BadRequestException maps to HTTP 400 via CustomExceptionHandler; a bare ArgumentException
        // would fall through to the 500 default and leak the message.
        if (string.IsNullOrWhiteSpace(orderBy))
            throw new BadRequestException("orderBy clause must not be empty.");

        if (_orderByInjectionPattern.IsMatch(orderBy))
            throw new BadRequestException("Invalid orderBy clause.");
    }

    /// <summary>
    /// Executes a paginated query and returns a PaginatedResult.
    /// Uses the scope-shared connection from the factory.
    /// </summary>
    public static Task<PaginatedResult<T>> QueryPaginatedAsync<T>(
        this ISqlConnectionFactory connectionFactory,
        string sql,
        string orderBy,
        PaginationRequest request,
        object? param = null)
        => connectionFactory.GetOpenConnection().QueryPaginatedAsync<T>(sql, null, orderBy, request, param);

    /// <summary>
    /// Paginated query with an OPTIONAL custom count statement. When <paramref name="countSql"/>
    /// is non-null it is used verbatim for the total count instead of wrapping
    /// <paramref name="sql"/> in <c>SELECT COUNT(*) FROM (…)</c>. Use this to count off a cheap
    /// base table (e.g. against the same parameters) when the data query reads an expensive
    /// view whose enrichment the count doesn't need.
    /// </summary>
    public static Task<PaginatedResult<T>> QueryPaginatedAsync<T>(
        this ISqlConnectionFactory connectionFactory,
        string sql,
        string? countSql,
        string orderBy,
        PaginationRequest request,
        object? param = null)
        => connectionFactory.GetOpenConnection().QueryPaginatedAsync<T>(sql, countSql, orderBy, request, param);

    /// <summary>
    /// Executes a paginated query on a caller-supplied connection. Use this overload
    /// when running multiple queries in parallel (`Task.WhenAll`) — each parallel
    /// query needs its own connection because the scope-shared one doesn't enable
    /// MultipleActiveResultSets. Pair with `ISqlConnectionFactory.CreateNewConnection()`
    /// and `using var conn = …` for proper disposal.
    /// </summary>
    public static Task<PaginatedResult<T>> QueryPaginatedAsync<T>(
        this IDbConnection connection,
        string sql,
        string orderBy,
        PaginationRequest request,
        object? param = null)
        => connection.QueryPaginatedAsync<T>(sql, null, orderBy, request, param);

    /// <summary>
    /// Paginated query on a caller-supplied connection with an OPTIONAL custom count
    /// statement (see the factory overload for semantics).
    /// </summary>
    public static async Task<PaginatedResult<T>> QueryPaginatedAsync<T>(
        this IDbConnection connection,
        string sql,
        string? countSql,
        string orderBy,
        PaginationRequest request,
        object? param = null)
    {
        ValidateOrderBy(orderBy);

        // Count query — use the caller-supplied cheap count when provided.
        var effectiveCountSql = countSql ?? $"SELECT COUNT(*) FROM ({sql}) AS CountQuery";
        var count = await connection.ExecuteScalarAsync<int>(effectiveCountSql, param);

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
        ValidateOrderBy(orderBy);
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
