using Auth.Application.Services;
using Auth.Infrastructure;

namespace Auth.Application.Features.Teams.GetTeamById;

public class GetTeamByIdQueryHandler(ITeamService teamService, AuthDbContext dbContext)
    : IQueryHandler<GetTeamByIdQuery, GetTeamByIdResult>
{
    public async Task<GetTeamByIdResult> Handle(GetTeamByIdQuery query, CancellationToken cancellationToken)
    {
        var team = await teamService.GetTeamById(query.Id, cancellationToken);
        if (team is null) throw new KeyNotFoundException($"Team {query.Id} not found.");

        var members = await dbContext.TeamMembers
            .Where(m => m.TeamId == team.Id)
            .Join(dbContext.Users, m => m.UserId, u => u.Id,
                (m, u) => new TeamMemberDto(u.Id, u.UserName!, u.FirstName, u.LastName))
            .ToListAsync(cancellationToken);

        return new GetTeamByIdResult(
            team.Id, team.Name, team.Type, team.IsActive,
            members);
    }
}
