namespace Request.Application.Features.Requests.GetRequestById;

internal class GetRequestByIdQueryHandler(RequestDbContext dbContext)
    : IQueryHandler<GetRequestByIdQuery, GetRequestByIdResult>
{
    public async Task<GetRequestByIdResult> Handle(GetRequestByIdQuery query, CancellationToken cancellationToken)
    {
        var request = await dbContext.Requests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == query.Id, cancellationToken);

        if (request is null) throw new RequestNotFoundException(query.Id);

        // Query titles (separate aggregate)
        var titles = await dbContext.RequestTitles
            .AsNoTracking()
            .Where(t => t.RequestId == query.Id)
            .ToListAsync(cancellationToken);

        return new GetRequestByIdResult
        {
            Id = request.Id,
            RequestNumber = request.RequestNumber ?? "",
            Status = request.Status,
            Purpose = request.Purpose,
            Channel = request.Channel,
            Requestor = new UserInfoDto(request.Requestor.UserId, request.Requestor.Username),
            Creator = new UserInfoDto(request.Creator.UserId, request.Creator.Username),
            Priority = request.Priority,
            IsPma = request.IsPma,
            Detail = request.Detail?.ToDto(),
            Customers = request.Customers.Select(c => c.ToDto()).ToList(),
            Properties = request.Properties.Select(p => p.ToDto()).ToList(),
            Titles = titles.Select(t => t.ToDto()).ToList(),
            Documents = request.Documents.Select(d => d.ToDto()).ToList()
        };
    }
}