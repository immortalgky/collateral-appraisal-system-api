namespace Auth.Application.Features.Roles.GetRoleUsers;

public record GetRoleUsersResult(List<RoleUserDto> Users);

public record RoleUserDto(Guid Id, string Username, string FirstName, string LastName, string? Email);
