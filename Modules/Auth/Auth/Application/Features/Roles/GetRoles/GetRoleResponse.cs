namespace Auth.Domain.Roles.Features.GetRoles;

public record GetRoleResponse(PaginatedResult<RoleDto> Result);
