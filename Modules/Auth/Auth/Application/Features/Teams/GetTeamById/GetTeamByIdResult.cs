namespace Auth.Application.Features.Teams.GetTeamById;

public record TeamMemberDto(Guid UserId, string UserName, string FirstName, string LastName);

public record GetTeamByIdResult(
    Guid Id,
    string Name,
    string Scope,
    string? Description,
    List<TeamMemberDto> Members);
