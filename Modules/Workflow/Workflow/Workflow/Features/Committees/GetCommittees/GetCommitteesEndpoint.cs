using Workflow.Domain.Committees;

namespace Workflow.Workflow.Features.Committees.GetCommittees;

public class GetCommitteesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workflows/committees", async (
                ISender sender,
                CancellationToken ct) =>
            {
                var query = new GetCommitteesQuery();
                var result = await sender.Send(query, ct);
                return Results.Ok(result);
            })
            .WithName("GetCommittees")
            .WithTags("Committees")
            .RequireAuthorization()
            .Produces<GetCommitteesResponse>();
    }
}

public record GetCommitteesQuery : IQuery<GetCommitteesResponse>;

public record GetCommitteesResponse(List<CommitteeListItem> Committees);

public record CommitteeListItem(
    Guid Id, string Name, string Code, string? Description,
    bool IsActive, string QuorumType, int QuorumValue, string MajorityType,
    int MemberCount);

public class GetCommitteesQueryHandler(
    ICommitteeRepository committeeRepository
) : IQueryHandler<GetCommitteesQuery, GetCommitteesResponse>
{
    public async Task<GetCommitteesResponse> Handle(GetCommitteesQuery query, CancellationToken ct)
    {
        var committees = await committeeRepository.GetActiveCommitteesAsync(ct);

        var items = committees.Select(c => new CommitteeListItem(
            c.Id, c.Name, c.Code, c.Description, c.IsActive,
            c.QuorumType.ToString(), c.QuorumValue, c.MajorityType.ToString(),
            c.GetActiveMembers().Count)).ToList();

        return new GetCommitteesResponse(items);
    }
}
