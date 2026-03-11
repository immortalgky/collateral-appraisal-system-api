using System.Text;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Common.Application.Features.Search.GlobalSearch;

public class GlobalSearchQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GlobalSearchQuery, GlobalSearchResult>
{
    private static readonly HashSet<string> ValidFilters = ["all", "requests", "customers", "properties"];

    public async Task<GlobalSearchResult> Handle(GlobalSearchQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter.ToLowerInvariant();
        var searchTerm = $"%{request.Q}%";
        var limit = Math.Clamp(request.Limit, 1, 20);

        var parameters = new DynamicParameters();
        parameters.Add("SearchTerm", searchTerm);
        parameters.Add("Limit", limit);

        var requests = new List<SearchResultItem>();
        var customers = new List<SearchResultItem>();
        var properties = new List<SearchResultItem>();

        using var connection = connectionFactory.GetOpenConnection();

        if (filter is "all" or "requests")
        {
            var rows = await connection.QueryAsync<SearchRow>(RequestsSql, parameters);
            requests = rows.Select(MapRequest).ToList();
        }

        if (filter is "all" or "customers")
        {
            var rows = await connection.QueryAsync<SearchRow>(CustomersSql, parameters);
            customers = rows.Select(MapCustomer).ToList();
        }

        if (filter is "all" or "properties")
        {
            var rows = await connection.QueryAsync<SearchRow>(PropertiesSql, parameters);
            properties = rows.Select(MapProperty).ToList();
        }

        var totalCount = requests.Count + customers.Count + properties.Count;
        var results = new GlobalSearchResults(requests, customers, properties);

        return new GlobalSearchResult(results, totalCount);
    }

    private static SearchResultItem MapRequest(SearchRow row)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["appraisalNumber"] = row.Title,
            ["status"] = row.Status,
            ["customerName"] = row.Subtitle,
            ["createdDate"] = row.Extra1
        };
        return new SearchResultItem(row.Id, row.Title, row.Subtitle, row.Status, "requests",
            $"/appraisals/{row.Id}", null, metadata);
    }

    private static SearchResultItem MapCustomer(SearchRow row)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["customerName"] = row.Title,
            ["phone"] = row.Subtitle
        };
        return new SearchResultItem(row.Id, row.Title, row.Subtitle, null, "customers",
            $"/requests/{row.Id}", null, metadata);
    }

    private static SearchResultItem MapProperty(SearchRow row)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["propertyType"] = row.Extra1,
            ["titleDeedNumber"] = row.Title,
            ["address"] = row.Subtitle
        };
        return new SearchResultItem(row.Id, row.Title, row.Subtitle, null, "properties",
            $"/requests/{row.Extra2}/titles/{row.Id}", null, metadata);
    }

    private const string RequestsSql = """
        SELECT TOP(@Limit)
            CAST(a.Id AS NVARCHAR(36)) AS Id,
            a.AppraisalNumber AS Title,
            (SELECT TOP 1 c.Name FROM request.RequestCustomers c WHERE c.RequestId = a.RequestId) AS Subtitle,
            a.Status,
            FORMAT(a.CreatedAt, 'yyyy-MM-dd') AS Extra1
        FROM appraisal.Appraisals a
        WHERE a.IsDeleted = 0
          AND (a.AppraisalNumber LIKE @SearchTerm
            OR EXISTS (SELECT 1 FROM request.RequestCustomers c2
                       WHERE c2.RequestId = a.RequestId AND c2.Name LIKE @SearchTerm))
        ORDER BY a.CreatedAt DESC
        """;

    private const string CustomersSql = """
        SELECT TOP(@Limit)
            CAST(c.RequestId AS NVARCHAR(36)) AS Id,
            c.Name AS Title,
            c.ContactNumber AS Subtitle,
            NULL AS Status,
            NULL AS Extra1
        FROM request.RequestCustomers c
        INNER JOIN request.Requests r ON r.Id = c.RequestId AND r.IsDeleted = 0
        WHERE c.Name LIKE @SearchTerm OR c.ContactNumber LIKE @SearchTerm
        ORDER BY c.Name
        """;

    private const string PropertiesSql = """
        SELECT TOP(@Limit)
            CAST(t.Id AS NVARCHAR(36)) AS Id,
            COALESCE(t.TitleNumber, t.CollateralType) AS Title,
            COALESCE(t.Province + ' ' + t.District, t.Province, '') AS Subtitle,
            NULL AS Status,
            t.CollateralType AS Extra1,
            CAST(t.RequestId AS NVARCHAR(36)) AS Extra2
        FROM request.RequestTitles t
        INNER JOIN request.Requests r ON r.Id = t.RequestId AND r.IsDeleted = 0
        WHERE t.TitleNumber LIKE @SearchTerm
           OR t.OwnerName LIKE @SearchTerm
           OR t.Province LIKE @SearchTerm
           OR t.District LIKE @SearchTerm
        ORDER BY t.Id DESC
        """;

    private class SearchRow
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Subtitle { get; set; }
        public string? Status { get; set; }
        public string? Extra1 { get; set; }
        public string? Extra2 { get; set; }
    }
}
