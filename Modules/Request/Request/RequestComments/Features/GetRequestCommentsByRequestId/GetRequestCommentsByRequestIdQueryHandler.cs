namespace Request.RequestComments.Features.GetRequestCommentsByRequestId;

internal class GetRequestCommentsByRequestIdQueryHandler(IRequestCommentReadRepository readRepository)
    : IQueryHandler<GetRequestCommentsByRequestIdQuery, GetRequestCommentsByRequestIdResult>
{
    public async Task<GetRequestCommentsByRequestIdResult> Handle(GetRequestCommentsByRequestIdQuery query,
        CancellationToken cancellationToken)
    {
        var requestComments = await readRepository.FindAsync(rc => rc.RequestId == query.RequestId, cancellationToken);

        var comments = requestComments
            .OrderBy(rc => rc.CreatedOn)
            .Select(rc => new RequestCommentDto
            {
                Id = rc.Id,
                RequestId = rc.RequestId,
                Comment = rc.Comment,
                CommentedBy = rc.CommentedBy,
                CommentedByName = rc.CommentedByName,
                CommentedAt = rc.CommentedAt
            })
            .ToList();

        return new GetRequestCommentsByRequestIdResult(comments);
    }
}