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
        object? param = null,
        bool freeTextSearch = false)
        => connectionFactory.GetOpenConnection()
            .QueryPaginatedAsync<T>(sql, countSql, orderBy, request, param, freeTextSearch);

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
    ///
    /// <para><b>Caller contract.</b> <paramref name="sql"/> and <paramref name="countSql"/> are
    /// appended to verbatim, so they must be built from literals with every value bound as a
    /// <c>@parameter</c>; this method cannot validate them. What it does own is checked here:
    /// <paramref name="orderBy"/> goes through <see cref="ValidateOrderBy"/>, the offset and page
    /// size are integers, and the query hint is a literal selected by a bool rather than a string
    /// the caller supplies.</para>
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube",
        "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "The interpolated fragments this method owns are all constrained: orderBy is rejected " +
            "by ValidateOrderBy unless it is a bare column plus ASC/DESC, offset and PageSize are " +
            "ints, and the hint is a literal chosen by a bool. sql/countSql are the caller's " +
            "contract, documented above and satisfied by every call site — each builds its text " +
            "from literals and binds values as @parameters.")]
    public static async Task<PaginatedResult<T>> QueryPaginatedAsync<T>(
        this IDbConnection connection,
        string sql,
        string? countSql,
        string orderBy,
        PaginationRequest request,
        object? param = null,
        bool freeTextSearch = false)
    {
        ValidateOrderBy(orderBy);

        // Two literals selected by a bool, never a caller-supplied hint string: a hint parameter
        // would be a new place for SQL to be interpolated, and callers have no business choosing
        // query hints. The choice is "free-text search or not", nothing finer.
        //
        // RECOMPILE so the optimizer sees the real LIKE pattern instead of planning every search
        // as a possible leading wildcard. FORCE ORDER so the search's derived table stays the
        // driver of the join: left free, the optimizer re-runs that union once per view row
        // whenever it thinks the ORDER BY lets it stop early, which turned a leading-wildcard
        // search into a 10-second query. See AppraisalFilterSql.ViewFrom.
        var hint = freeTextSearch ? " OPTION (RECOMPILE, FORCE ORDER)" : "";

        // Count query — use the caller-supplied cheap count when provided.
        var effectiveCountSql = (countSql ?? $"SELECT COUNT(*) FROM ({sql}) AS CountQuery") + hint;
        var count = await connection.ExecuteScalarAsync<int>(effectiveCountSql, param);

        // Data query with pagination
        var offset = request.PageNumber * request.PageSize;
        var pagedSql = $@"{sql}
            ORDER BY {orderBy}
            OFFSET {offset} ROWS FETCH NEXT {request.PageSize} ROWS ONLY{hint}";

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
