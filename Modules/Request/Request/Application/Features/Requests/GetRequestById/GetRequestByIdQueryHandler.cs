using Auth.Contracts.Users;

namespace Request.Application.Features.Requests.GetRequestById;

internal class GetRequestByIdQueryHandler(
    RequestDbContext dbContext,
    IUserLookupService userLookupService)
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

        // Resolve requestor org detail on read from the stored employee code — not snapshotted.
        // Falls back to the stored identity if the user can no longer be resolved.
        var requestorInfo = await userLookupService.GetRequestorAsync(request.Requestor.UserId, cancellationToken);

        return new GetRequestByIdResult
        {
            Id = request.Id,
            RequestNumber = request.RequestNumber ?? "",
            Status = request.Status,
            Purpose = request.Purpose,
            Channel = request.Channel,
            RequestedAt = request.RequestedAt,
            Requestor = new RequestorDetailDto(
                EmployeeId: request.Requestor.UserId,
                Name: requestorInfo?.Name ?? request.Requestor.Username,
                Email: requestorInfo?.Email,
                ContactNo: requestorInfo?.ContactNo,
                AoCode: requestorInfo?.AoCode,
                CostCenterCode: requestorInfo?.CostCenterCode,
                CostCenterDescription: requestorInfo?.CostCenterDescription,
                Department: requestorInfo?.Department),
            Creator = new UserInfoDto(request.Creator.UserId, request.Creator.Username),
            Priority = request.Priority?.Code,
            IsPma = request.IsPma,
            Detail = request.Detail?.ToDto(),
            Customers = request.Customers.Select(c => c.ToDto()).ToList(),
            Properties = request.Properties.Select(p => p.ToDto()).ToList(),
            Titles = titles.Select(t => t.ToDto()).ToList(),
            Documents = request.Documents.Select(d => d.ToDto()).ToList()
        };
    }
}