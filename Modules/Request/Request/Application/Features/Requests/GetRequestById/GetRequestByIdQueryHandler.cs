using Dapper;
using Request.Application.ReadModels;

namespace Request.Application.Features.Requests.GetRequestById;

internal class GetRequestByIdQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetRequestByIdQuery, GetRequestByIdResult>

{
    public async Task<GetRequestByIdResult> Handle(GetRequestByIdQuery query, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT *
                           FROM [request].[vw_Requests]
                           WHERE [Id] = @Id

                           SELECT *
                           FROM [request].[vw_RequestCustomers]
                           WHERE [RequestId] = @Id

                           SELECT *
                           FROM [request].[vw_RequestProperties]
                           WHERE [RequestId] = @Id

                           SELECT *
                           FROM [request].[vw_RequestDocuments]
                           WHERE [RequestId] = @Id

                           SELECT *
                           FROM [request].[RequestTitles]
                           WHERE [RequestId] = @Id

                           SELECT td.*
                           FROM [request].[RequestTitleDocuments] td
                           INNER JOIN [request].[RequestTitles] t ON td.TitleId = t.Id
                           WHERE [RequestId] = @Id
                           """;

        var connection = connectionFactory.GetOpenConnection();

        await using var multi = await connection.QueryMultipleAsync(sql, new { query.Id });

        var row = await multi.ReadFirstOrDefaultAsync<RequestRow>();

        if (row is null) throw new RequestNotFoundException(query.Id);

        var customersRow = (await multi.ReadAsync<RequestCustomerRow>()).ToList();
        var propertiesRow = (await multi.ReadAsync<RequestPropertyRow>()).ToList();
        var documentsRow = (await multi.ReadAsync<RequestDocumentRow>()).ToList();
        var titlesRow = (await multi.ReadAsync<RequestTitleRow>()).ToList();
        var titleDocsRow = (await multi.ReadAsync<RequestTitleDocumentRow>()).ToList();

        var titleDocsByTitleId = titleDocsRow
            .GroupBy(td => td.TitleId)
            .ToDictionary(g => g.Key, g => g.Adapt<List<RequestTitleDocumentDto>>());

        var titles = titlesRow.Select(t =>
        {
            var docs = titleDocsByTitleId.TryGetValue(t.Id, out var d) ? d : [];
            return t.Adapt<RequestTitleDto>() with { Documents = docs };
        }).ToList();

        var result = new GetRequestByIdResult
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
            Customers = customersRow.Adapt<List<RequestCustomerDto>>(),
            Properties = propertiesRow.Adapt<List<RequestPropertyDto>>(),
            Titles = titles,
            Documents = documentsRow.Adapt<List<RequestDocumentDto>>()
        };

        return result;
    }
}