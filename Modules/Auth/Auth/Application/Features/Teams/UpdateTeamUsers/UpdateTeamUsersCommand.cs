namespace Auth.Application.Features.Teams.UpdateTeamUsers;

public record UpdateTeamUsersCommand(Guid Id, List<Guid> MemberUserIds) : ICommand;
