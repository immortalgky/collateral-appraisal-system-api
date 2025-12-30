using Dapper;
using Request.Application.ReadModels;
using Shared.Pagination;

namespace Request.Application.Features.Requests.GetRequests;

public class GetRequestQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetRequestQuery, GetRequestResult>
{
    public async Task<GetRequestResult> Handle(GetRequestQuery query, CancellationToken cancellationToken)
    {
        // Paginate main requests
        const string baseSql = "SELECT * FROM [request].[vw_Requests]";

        var paginatedRows = await connectionFactory.QueryPaginatedAsync<RequestRow>(
            baseSql, "Id", query.PaginationRequest);

        var requestRows = paginatedRows.Items.ToList();

        if (requestRows.Count == 0)
            return new GetRequestResult(new PaginatedResult<RequestDto>(
                [], paginatedRows.Count, paginatedRows.PageNumber, paginatedRows.PageSize));

        // Get all request IDs
        var requestIds = requestRows.Select(r => r.Id).ToArray();

        // Fetch related data
        const string sql = """
                           SELECT * FROM [request].[vw_RequestCustomers] WHERE [RequestId] IN @Ids

                           SELECT * FROM [request].[vw_RequestProperties] WHERE [RequestId] IN @Ids

                           SELECT * FROM [request].[vw_RequestDocuments] WHERE [RequestId] IN @Ids

                           SELECT * FROM [request].[RequestTitles] WHERE [RequestId] IN @Ids

                           SELECT td.* FROM [request].[RequestTitleDocuments] td
                           INNER JOIN [request].[RequestTitles] t ON td.TitleId = t.Id
                           WHERE t.[RequestId] IN @Ids
                           """;

        var connection = connectionFactory.GetOpenConnection();
        await using var multi = await connection.QueryMultipleAsync(sql, new { Ids = requestIds });

        var customersRows = (await multi.ReadAsync<RequestCustomerRow>()).ToList();
        var propertiesRows = (await multi.ReadAsync<RequestPropertyRow>()).ToList();
        var documentsRows = (await multi.ReadAsync<RequestDocumentRow>()).ToList();
        var titlesRows = (await multi.ReadAsync<RequestTitleRow>()).ToList();
        var titleDocsRows = (await multi.ReadAsync<RequestTitleDocumentRow>()).ToList();

        // Group by RequestId
        var customersByRequestId = customersRows.GroupBy(c => c.RequestId)
            .ToDictionary(g => g.Key, g => g.Adapt<List<RequestCustomerDto>>());
        var propertiesByRequestId = propertiesRows.GroupBy(p => p.RequestId)
            .ToDictionary(g => g.Key, g => g.Adapt<List<RequestPropertyDto>>());
        var documentsByRequestId = documentsRows.GroupBy(d => d.RequestId)
            .ToDictionary(g => g.Key, g => g.Adapt<List<RequestDocumentDto>>());

        // Group title docs by TitleId
        var titleDocsByTitleId = titleDocsRows.GroupBy(td => td.TitleId)
            .ToDictionary(g => g.Key, g => g.Adapt<List<RequestTitleDocumentDto>>());

        // Build titles with their documents
        var titlesByRequestId = titlesRows.GroupBy(t => t.RequestId)
            .ToDictionary(g => g.Key, g => g.Select(t =>
            {
                var docs = titleDocsByTitleId.TryGetValue(t.Id, out var d) ? d : [];
                return t.Adapt<RequestTitleDto>() with { Documents = docs };
            }).ToList());

        // Map to DTOs
        var items = requestRows.Select(row => new RequestDto
        {
            Id = row.Id,
            RequestNumber = row.RequestNumber,
            Status = row.Status,
            Purpose = row.Purpose,
            Channel = row.Channel,
            Requestor = new UserInfoDto(row.Requestor, row.RequestorName),
            Creator = new UserInfoDto(row.Creator, row.CreatorName),
            Priority = row.Priority,
            IsPma = row.IsPma,
            Detail = row.Adapt<RequestDetailDto>(),
            Customers = customersByRequestId.TryGetValue(row.Id, out var c) ? c : [],
            Properties = propertiesByRequestId.TryGetValue(row.Id, out var p) ? p : [],
            Documents = documentsByRequestId.TryGetValue(row.Id, out var doc) ? doc : [],
            Titles = titlesByRequestId.TryGetValue(row.Id, out var t) ? t : []
        }).ToList();

        return new GetRequestResult(new PaginatedResult<RequestDto>(
            items, paginatedRows.Count, paginatedRows.PageNumber, paginatedRows.PageSize));
    }
}