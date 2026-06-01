using Dapper;
using Shared.Pagination;

namespace Request.Application.Features.Reappraisal.GetCandidates;

/// <summary>
/// Read-side handler for the Reappraisal Candidates list page.
/// Uses Dapper + DynamicParameters against vw_ReappraisalCandidates (Status &lt;&gt; 'Deleted').
/// </summary>
public class GetReappraisalCandidatesQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetReappraisalCandidatesQuery, GetReappraisalCandidatesResult>
{
    public async Task<GetReappraisalCandidatesResult> Handle(
        GetReappraisalCandidatesQuery query,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT
                c.Id,
                c.Status,
                c.ReviewType,
                c.AppraisalDate,
                c.RemainingDay,
                c.OldAppraisalReportNumber,
                c.CifNumber,
                c.CustomerName,
                c.CollateralId,
                c.CollateralName,
                c.CurrentValue,
                c.HasOpenAppraisal,
                c.OpenAppraisalId,
                c.OpenAppraisalNumber,
                c.OpenAppraisalGroupTag
            FROM request.vw_ReappraisalCandidates c
            WHERE c.Status = 'Pending'
            """;

        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.CustomerName))
        {
            sql += " AND c.CustomerName LIKE @CustomerName";
            p.Add("CustomerName", $"%{query.CustomerName.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(query.OldAppraisalReportNumber))
        {
            sql += " AND c.OldAppraisalReportNumber LIKE @OldAppraisalReportNumber";
            p.Add("OldAppraisalReportNumber", $"%{query.OldAppraisalReportNumber.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(query.CifNumber))
        {
            sql += " AND c.CifNumber = @CifNumber";
            p.Add("CifNumber", query.CifNumber.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.CollateralId))
        {
            sql += " AND c.CollateralId = @CollateralId";
            p.Add("CollateralId", query.CollateralId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.ReviewType))
        {
            sql += " AND c.ReviewType = @ReviewType";
            p.Add("ReviewType", query.ReviewType.Trim());
        }

        if (query.ReviewDateFrom.HasValue)
        {
            sql += " AND c.ReviewDate >= @ReviewDateFrom";
            // DateOnly → DateTime for Dapper (see feedback_dapper_dateonly)
            p.Add("ReviewDateFrom", query.ReviewDateFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (query.ReviewDateTo.HasValue)
        {
            sql += " AND c.ReviewDate <= @ReviewDateTo";
            p.Add("ReviewDateTo", query.ReviewDateTo.Value.ToDateTime(TimeOnly.MaxValue));
        }

        if (query.RemainingDayFrom.HasValue)
        {
            sql += " AND c.RemainingDay >= @RemainingDayFrom";
            p.Add("RemainingDayFrom", query.RemainingDayFrom.Value);
        }

        if (query.RemainingDayTo.HasValue)
        {
            sql += " AND c.RemainingDay <= @RemainingDayTo";
            p.Add("RemainingDayTo", query.RemainingDayTo.Value);
        }

        var result = await connectionFactory.GetOpenConnection()
            .QueryPaginatedAsync<ReappraisalCandidateListItem>(
                sql,
                orderBy: "c.ReviewDate ASC, c.CifNumber ASC",
                request: query.Pagination,
                param: p);

        return new GetReappraisalCandidatesResult(result);
    }
}
