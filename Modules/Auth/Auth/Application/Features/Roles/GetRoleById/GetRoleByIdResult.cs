namespace Auth.Application.Features.Roles.GetRoleById;

public record GetRoleByIdResult(Guid Id, string Name, string Description, string? Scope, List<PermissionDto> Permissions);
