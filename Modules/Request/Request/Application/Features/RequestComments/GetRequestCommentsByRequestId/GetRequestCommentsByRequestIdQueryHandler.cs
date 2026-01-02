using Shared.Pagination;

namespace Request.Application.Features.RequestComments.GetRequestCommentsByRequestId;

internal class GetRequestCommentsByRequestIdQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetRequestCommentsByRequestIdQuery, GetRequestCommentsByRequestIdResult>
{
    public async Task<GetRequestCommentsByRequestIdResult> Handle(GetRequestCommentsByRequestIdQuery query,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT
                [Id],
                [RequestId],
                [Comment],
                [CommentedBy],
                [CommentedByName],
                [CommentedAt],
                [LastModifiedAt]
            FROM [request].[RequestComments]
            WHERE [RequestId] = @RequestId
            ORDER BY [CommentedAt] ASC
            """;

        var comments = await connectionFactory.QueryAsync<RequestCommentDto>(
            sql,
            new { query.RequestId });

        return new GetRequestCommentsByRequestIdResult(comments.ToList());
    }
}