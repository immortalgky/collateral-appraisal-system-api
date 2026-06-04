namespace Auth.Application.Features.Teams.GetTeams;

public record GetTeamsResponse(IEnumerable<TeamListItemDto> Items, long Count, int PageNumber, int PageSize);
