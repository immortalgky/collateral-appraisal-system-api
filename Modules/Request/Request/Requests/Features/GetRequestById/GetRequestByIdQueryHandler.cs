namespace Request.Requests.Features.GetRequestById;

internal class GetRequestByIdQueryHandler(IRequestReadRepository repository)
    : IQueryHandler<GetRequestByIdQuery, GetRequestByIdResult>
{
    public async Task<GetRequestByIdResult> Handle(GetRequestByIdQuery query, CancellationToken cancellationToken)
    {
        var request = await repository.GetByIdAsync(query.Id, cancellationToken);

        if (request is null) throw new RequestNotFoundException(query.Id);

        var result = request.Adapt<GetRequestByIdResult>();

        return result;
    }
}


//-------------------------------------------------------------------------------------------
// Example of using Dapper to query the database directly.
// Uncomment the following code if you want to use Dapper for raw SQL queries.
//-------------------------------------------------------------------------------------------
// var connection = connectionFactory.GetOpenConnection();
//
// return await connection.QuerySingleAsync<GetRequestByIdResult>(
//     $"""
//      SELECT
//         [Id] AS [{nameof(GetRequestByIdResult.Id)}],
//         [AppraisalNo] AS [{nameof(GetRequestByIdResult.AppraisalNo)}],
//         [Status] AS [{nameof(GetRequestByIdResult.Status)}]
//      FROM [request].[vw_Requests] AS [Requests]
//      WHERE [Id] = @Id
//      """,
//     new
//     {
//         Id = query.Id
//     });