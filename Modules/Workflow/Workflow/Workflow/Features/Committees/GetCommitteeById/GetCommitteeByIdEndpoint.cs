using Workflow.Domain.Committees;

namespace Workflow.Workflow.Features.Committees.GetCommitteeById;

public class GetCommitteeByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workflows/committees/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken ct) =>
            {
                var query = new GetCommitteeByIdQuery(id);
                var result = await sender.Send(query, ct);
                return Results.Ok(result);
            })
            .WithName("GetCommitteeById")
            .WithTags("Committees")
            .RequireAuthorization()
            .Produces<GetCommitteeByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public record GetCommitteeByIdQuery(Guid Id) : IQuery<GetCommitteeByIdResponse>;

public record GetCommitteeByIdResponse(
    Guid Id, string Name, string Code, string? Description,
    bool IsActive, string QuorumType, int QuorumValue, string MajorityType,
    List<CommitteeMemberDto> Members,
    List<CommitteeThresholdDto> Thresholds,
    List<CommitteeConditionDto> Conditions);

public record CommitteeMemberDto(Guid Id, string UserId, string MemberName, string Role, bool IsActive);
public record CommitteeThresholdDto(Guid Id, decimal? MinValue, decimal? MaxValue, int Priority, bool IsActive);
public record CommitteeConditionDto(Guid Id, string ConditionType, string? RoleRequired, int? MinVotesRequired, int Priority, bool IsActive, string? Description);

public class GetCommitteeByIdQueryHandler(
    ICommitteeRepository committeeRepository
) : IQueryHandler<GetCommitteeByIdQuery, GetCommitteeByIdResponse>
{
    public async Task<GetCommitteeByIdResponse> Handle(GetCommitteeByIdQuery query, CancellationToken ct)
    {
        var committee = await committeeRepository.GetByIdWithMembersAsync(query.Id, ct)
            ?? throw new NotFoundException($"Committee {query.Id} not found");

        return new GetCommitteeByIdResponse(
            committee.Id, committee.Name, committee.Code, committee.Description,
            committee.IsActive, committee.QuorumType.ToString(), committee.QuorumValue,
            committee.MajorityType.ToString(),
            committee.Members.Select(m => new CommitteeMemberDto(
                m.Id, m.UserId, m.MemberName, m.Role.ToString(), m.IsActive)).ToList(),
            committee.Thresholds.Select(t => new CommitteeThresholdDto(
                t.Id, t.MinValue, t.MaxValue, t.Priority, t.IsActive)).ToList(),
            committee.Conditions.Select(c => new CommitteeConditionDto(
                c.Id, c.ConditionType.ToString(), c.RoleRequired, c.MinVotesRequired,
                c.Priority, c.IsActive, c.Description)).ToList());
    }
}
