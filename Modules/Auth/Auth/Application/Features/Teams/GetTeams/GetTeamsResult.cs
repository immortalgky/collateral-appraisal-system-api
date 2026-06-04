namespace Auth.Application.Features.Teams.GetTeams;

public record TeamListItemDto(Guid Id, string Name, string Type, bool IsActive, int MemberCount);

public record GetTeamsResult(IEnumerable<TeamListItemDto> Items, long Count, int PageNumber, int PageSize);
