using Shared.Pagination;

namespace Request.Application.Features.RequestComments.GetRequestCommentById;

internal class GetRequestCommentByIdQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetRequestCommentByIdQuery, GetRequestCommentByIdResult>
{
    public async Task<GetRequestCommentByIdResult> Handle(GetRequestCommentByIdQuery query,
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
            WHERE [Id] = @CommentId AND [RequestId] = @RequestId
            """;

        var result = await connectionFactory.QueryFirstOrDefaultAsync<GetRequestCommentByIdResult>(
            sql,
            new { query.CommentId, query.RequestId });

        return result ?? throw new RequestCommentNotFoundException(query.CommentId);
    }
}