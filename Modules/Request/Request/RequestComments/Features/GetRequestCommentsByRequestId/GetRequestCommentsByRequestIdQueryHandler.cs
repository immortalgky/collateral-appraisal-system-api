namespace Request.RequestComments.Features.GetRequestCommentsByRequestId;

internal class GetRequestCommentsByRequestIdQueryHandler(IRequestCommentReadRepository readRepository)
    : IQueryHandler<GetRequestCommentsByRequestIdQuery, GetRequestCommentsByRequestIdResult>
{
    public async Task<GetRequestCommentsByRequestIdResult> Handle(GetRequestCommentsByRequestIdQuery query, CancellationToken cancellationToken)
    {
        var requestComments = await readRepository.FindAsync(rc => rc.RequestId == query.RequestId, cancellationToken);
        
        var comments = requestComments
            .OrderBy(rc => rc.CreatedOn)
            .Select(rc => new RequestCommentDto(
                rc.Id,
                rc.RequestId,
                rc.Comment,
                rc.CreatedOn,
                rc.CreatedBy,
                rc.UpdatedOn,
                rc.UpdatedBy
            ))
            .ToList();

        return new GetRequestCommentsByRequestIdResult(comments);
    }
}