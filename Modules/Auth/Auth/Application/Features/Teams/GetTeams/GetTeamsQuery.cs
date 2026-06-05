namespace Auth.Application.Features.Teams.GetTeams;

public record GetTeamsQuery(string? Search, string? Type, int PageNumber = 1, int PageSize = 20)
    : IQuery<GetTeamsResult>;
