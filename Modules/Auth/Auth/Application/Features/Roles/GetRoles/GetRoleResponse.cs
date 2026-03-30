namespace Auth.Application.Features.Roles.GetRoles;

public record GetRoleResponse(IEnumerable<RoleListItemDto> Items, long Count, int PageNumber, int PageSize);
