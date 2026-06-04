namespace Auth.Application.Features.Teams.GetTeamById;

public record GetTeamByIdQuery(Guid Id) : IQuery<GetTeamByIdResult>;
