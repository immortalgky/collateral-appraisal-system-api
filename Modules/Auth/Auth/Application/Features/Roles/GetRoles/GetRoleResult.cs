namespace Auth.Application.Features.Roles.GetRoles;

public record GetRoleResult(IEnumerable<RoleListItemDto> Items, long Count, int PageNumber, int PageSize);

public record RoleListItemDto(Guid Id, string Name, string Description, string? Scope, int PermissionCount);
