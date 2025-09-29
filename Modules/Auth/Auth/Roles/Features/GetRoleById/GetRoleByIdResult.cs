namespace Auth.Roles.Features.GetRoleById;

public record GetRoleByIdResult(Guid Id, string Name, List<PermissionDto> Permissions);
