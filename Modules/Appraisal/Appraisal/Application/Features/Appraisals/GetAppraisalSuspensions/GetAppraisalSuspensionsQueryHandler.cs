using Dapper;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalSuspensions;

public class GetAppraisalSuspensionsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetAppraisalSuspensionsQuery, PaginatedResult<AppraisalSuspensionRow>>
{
    private const int MaxPageSize = 100;

    // Shared by the count and the rows so they always agree.
    private const string BaseWhere = "ap.IsDeleted = 0 AND ap.Status = 'Completed'";

    private const string SelectSql = """
                                     SELECT ap.Id AS AppraisalId,
                                            ap.AppraisalNumber,
                                            c.Name AS CustomerName,
                                            v.AppraisedValue,
                                            ap.AppraisalType,
                                            ap.CompletedAt,
                                            ap.IsSuspended
                                     FROM appraisal.Appraisals ap
                                     LEFT JOIN appraisal.ValuationAnalyses v ON v.AppraisalId = ap.Id
                                     OUTER APPLY (SELECT TOP 1 Name
                                                  FROM request.RequestCustomers
                                                  WHERE RequestId = ap.RequestId
                                                  ORDER BY Id) c
                                     OUTER APPLY (SELECT TOP 1 ActionAt
                                                  FROM appraisal.AppraisalSuspensionLogs
                                                  WHERE AppraisalId = ap.Id
                                                  ORDER BY ActionAt DESC, Id DESC) l
                                     WHERE {0}
                                     ORDER BY l.ActionAt DESC, ap.CompletedAt DESC, ap.Id
                                     OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
                                     """;

    private const string CountSql = "SELECT COUNT(*) FROM appraisal.Appraisals ap WHERE {0};";

    // User text only travels as @Pattern, with its LIKE wildcards escaped.
    private const string ByNumber = " AND ap.AppraisalNumber LIKE @Pattern ESCAPE '\\'";
    private const string SuspendedOnly = " AND ap.IsSuspended = 1"; // IX_Appraisals_Suspended
    private const string ActiveOnly = " AND ap.IsSuspended = 0";

    public async Task<PaginatedResult<AppraisalSuspensionRow>> Handle(
        GetAppraisalSuspensionsQuery query, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(query.Pagination.PageNumber, 0);
        var pageSize = Math.Clamp(query.Pagination.PageSize, 1, MaxPageSize);
        var appraisalNumber = query.AppraisalNumber?.Trim();
        var hasNumber = !string.IsNullOrEmpty(appraisalNumber);

        // All / Active without a number would list nearly every Completed appraisal.
        if (!hasNumber && query.Status != SuspensionFilter.Suspended)
            return new PaginatedResult<AppraisalSuspensionRow>([], 0, pageNumber, pageSize);

        var where = BaseWhere
                    + (hasNumber ? ByNumber : "")
                    + query.Status switch
                    {
                        SuspensionFilter.Suspended => SuspendedOnly,
                        SuspensionFilter.Active => ActiveOnly,
                        _ => ""
                    };

        var parameters = new
        {
            Pattern = hasNumber ? "%" + EscapeLike(appraisalNumber!) + "%" : null,
            Skip = pageNumber * pageSize,
            Take = pageSize
        };

        var connection = connectionFactory.GetOpenConnection();

        var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            string.Format(CountSql, where), parameters, cancellationToken: cancellationToken));

        if (count == 0)
            return new PaginatedResult<AppraisalSuspensionRow>([], 0, pageNumber, pageSize);

        // Positional record: column order in SelectSql must match the constructor.
        var rows = await connection.QueryAsync<AppraisalSuspensionRow>(new CommandDefinition(
            string.Format(SelectSql, where), parameters, cancellationToken: cancellationToken));

        return new PaginatedResult<AppraisalSuspensionRow>(rows.ToList(), count, pageNumber, pageSize);
    }

    // Escape the escape character first, then the wildcards.
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
