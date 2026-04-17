using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

/// <summary>
/// Handler for getting all Appraisals with pagination, filtering, sorting, and facets.
/// Uses SQL view + Dapper for efficient read queries.
/// </summary>
public class GetAppraisalsQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetAppraisalsQuery, GetAppraisalsResult>
{
    public async Task<GetAppraisalsResult> Handle(
        GetAppraisalsQuery query,
        CancellationToken cancellationToken)
    {
        var filter = query.Filter;
        var (whereClause, parameters) = AppraisalFilterBuilder.BuildFilter(filter);
        var orderBy = AppraisalFilterBuilder.BuildOrderBy(filter);

        var baseSql = "SELECT * FROM appraisal.vw_AppraisalList" + whereClause;

        // Execute paginated query
        var result = await connectionFactory.QueryPaginatedAsync<AppraisalDto>(
            baseSql,
            orderBy,
            query.PaginationRequest,
            parameters);

        // Execute facet counts in a single pass (not 5x UNION ALL)
        var facetsSql = $"""
            SELECT Status, SLAStatus, Priority, AppraisalType, AssignmentType
            FROM appraisal.vw_AppraisalList{whereClause}
            """;

        var connection = connectionFactory.GetOpenConnection();
        var facetRows = await connection.QueryAsync<FacetRawRow>(facetsSql, parameters);
        var facetList = facetRows.ToList();

        var facets = BuildFacets(facetList);

        return new GetAppraisalsResult(result, facets);
    }

    private static AppraisalFacets BuildFacets(List<FacetRawRow> rows)
    {
        return new AppraisalFacets
        {
            Status = GroupCount(rows, r => r.Status),
            SlaStatus = GroupCount(rows, r => r.SLAStatus),
            Priority = GroupCount(rows, r => r.Priority),
            AppraisalType = GroupCount(rows, r => r.AppraisalType),
            AssignmentType = GroupCount(rows, r => r.AssignmentType)
        };
    }

    private static List<FacetItem> GroupCount(List<FacetRawRow> rows, Func<FacetRawRow, string?> selector)
    {
        return rows
            .Select(selector)
            .Where(v => v is not null)
            .GroupBy(v => v!)
            .Select(g => new FacetItem(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)
            .ToList();
    }

    private class FacetRawRow
    {
        public string Status { get; set; } = "";
        public string? SLAStatus { get; set; }
        public string Priority { get; set; } = "";
        public string AppraisalType { get; set; } = "";
        public string? AssignmentType { get; set; }
    }
}
