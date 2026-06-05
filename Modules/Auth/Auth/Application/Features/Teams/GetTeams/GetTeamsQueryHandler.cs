using Auth.Application.Services;
using Shared.Pagination;

namespace Auth.Application.Features.Teams.GetTeams;

public class GetTeamsQueryHandler(ITeamService teamService)
    : IQueryHandler<GetTeamsQuery, GetTeamsResult>
{
    public async Task<GetTeamsResult> Handle(GetTeamsQuery query, CancellationToken cancellationToken)
    {
        var paginationRequest = new PaginationRequest(query.PageNumber - 1, query.PageSize);
        var paginated = await teamService.GetTeams(query.Search, query.Type, paginationRequest, cancellationToken);

        var items = paginated.Items.Select(t => new TeamListItemDto(
            t.Id, t.Name, t.Type, t.IsActive, t.Members.Count));

        return new GetTeamsResult(items, paginated.Count, paginated.PageNumber, paginated.PageSize);
    }
}
