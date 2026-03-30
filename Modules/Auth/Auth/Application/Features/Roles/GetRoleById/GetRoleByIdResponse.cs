namespace Auth.Application.Features.Roles.GetRoleById;

public record GetRoleByIdResponse(Guid Id, string Name, string Description, string? Scope, List<PermissionDto> Permissions);
