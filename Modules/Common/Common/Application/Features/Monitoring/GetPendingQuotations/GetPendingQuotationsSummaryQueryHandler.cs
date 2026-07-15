using Common.Application.Features.Monitoring.Shared;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Common.Application.Features.Monitoring.GetPendingQuotations;

/// <summary>
/// Returns a Total count for the Pending Quotations monitoring tab.
/// appraisal.vw_QuotationList does not expose OlaVarianceHours/OlaTargetHours,
/// so Breached/AtRisk/Healthy bucket fields are null — only Total is returned.
/// The same terminal-status exclusion as the list handler is applied by default.
/// </summary>
public class GetPendingQuotationsSummaryQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetPendingQuotationsSummaryQuery, MonitoringSummaryDto>
{
    public async Task<MonitoringSummaryDto> Handle(
        GetPendingQuotationsSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        var filter = query.Filter;
        if (filter.Status is { Length: > 0 })
        {
            conditions.Add("q.Status IN @Statuses");
            parameters.Add("Statuses", filter.Status);
        }
        else
        {
            conditions.Add("q.Status NOT IN ('Closed', 'Finalized', 'Cancelled')");
        }

        // Per-field search — keep the count consistent with the list handler's predicates.
        if (!string.IsNullOrWhiteSpace(filter.QuotationNo))
        {
            conditions.Add(@"q.QuotationNumber LIKE @QuotationNoPattern ESCAPE '\'");
            parameters.Add("QuotationNoPattern", "%" + EscapeLike(filter.QuotationNo.Trim()) + "%");
        }

        if (!string.IsNullOrWhiteSpace(filter.AppraisalNo))
        {
            conditions.Add(@"EXISTS (
    SELECT 1 FROM appraisal.QuotationRequestAppraisals qra
    JOIN appraisal.Appraisals a ON a.Id = qra.AppraisalId
    WHERE qra.QuotationRequestId = q.Id
      AND a.AppraisalNumber LIKE @AppraisalNoPattern ESCAPE '\')");
            parameters.Add("AppraisalNoPattern", "%" + EscapeLike(filter.AppraisalNo.Trim()) + "%");
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
        {
            conditions.Add(@"EXISTS (
    SELECT 1 FROM appraisal.QuotationRequestAppraisals qra
    JOIN appraisal.Appraisals a ON a.Id = qra.AppraisalId
    JOIN request.RequestCustomers rc ON rc.RequestId = a.RequestId
    WHERE qra.QuotationRequestId = q.Id
      AND rc.Name LIKE @CustomerNamePattern ESCAPE '\')");
            parameters.Add("CustomerNamePattern", "%" + EscapeLike(filter.CustomerName.Trim()) + "%");
        }

        // Keep the count consistent with the list handler's invited-company predicate.
        if (!string.IsNullOrWhiteSpace(filter.AppraisalCompanyId))
        {
            conditions.Add(@"EXISTS (
    SELECT 1 FROM appraisal.QuotationInvitations qi
    WHERE qi.QuotationRequestId = q.Id
      AND qi.CompanyId = @AppraisalCompanyId)");
            parameters.Add("AppraisalCompanyId", filter.AppraisalCompanyId);
        }

        var where = "WHERE " + string.Join(" AND ", conditions);
        var sql = $"SELECT COUNT(*) FROM appraisal.vw_QuotationList q {where}";

        var conn = connectionFactory.GetOpenConnection();
        var total = await conn.ExecuteScalarAsync<int>(sql, parameters);
        return new MonitoringSummaryDto(total, null, null, null);
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
