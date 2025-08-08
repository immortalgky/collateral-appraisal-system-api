namespace Request.RequestComments.Features.GetRequestCommentById;

internal class GetRequestCommentByIdQueryHandler(IRequestCommentReadRepository readRepository)
    : IQueryHandler<GetRequestCommentByIdQuery, GetRequestCommentByIdResult>
{
    public async Task<GetRequestCommentByIdResult> Handle(GetRequestCommentByIdQuery query, CancellationToken cancellationToken)
    {
        var requestComment = await readRepository.FirstOrDefaultAsync(rc => rc.Id == query.CommentId && rc.RequestId == query.RequestId, cancellationToken);

        if (requestComment is null)
            throw new RequestCommentNotFoundException(query.CommentId);

        var result = new GetRequestCommentByIdResult(
            requestComment.Id,
            requestComment.RequestId,
            requestComment.Comment,
            requestComment.CreatedOn,
            requestComment.CreatedBy,
            requestComment.UpdatedOn,
            requestComment.UpdatedBy
        );

        return result;
    }
}