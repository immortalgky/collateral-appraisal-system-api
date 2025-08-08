namespace Request.Requests.Features.GetRequestById;

internal class GetRequestByIdHandler(RequestDbContext dbContext)
    : IQueryHandler<GetRequestByIdQuery, GetRequestByIdResult>
{
    public async Task<GetRequestByIdResult> Handle(GetRequestByIdQuery query, CancellationToken cancellationToken)
    {
        var request = await dbContext.Requests.FindAsync([query.Id], cancellationToken);

        if (request is null) throw new RequestNotFoundException(query.Id);

        var result = request.Adapt<GetRequestByIdResult>();

        return result;
    }
}