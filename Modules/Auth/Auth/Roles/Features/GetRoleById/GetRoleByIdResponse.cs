namespace Auth.Roles.Features.GetRoleById;

public record GetRoleByIdResponse(Guid Id, string Name, List<PermissionDto> Permissions);
