using OAuth2OpenId.Identity.Models;

namespace Auth.Extensions;

public static class DtoExtensions
{
    public static PermissionDto ToDto(this Permission domain)
    {
        return new PermissionDto(domain.Id, domain.PermissionCode, domain.Description);
    }

    public static RoleDto ToDto(this ApplicationRole domain)
    {
        return new RoleDto(
            domain.Id,
            domain.Name ?? "",
            domain.Description,
            [.. domain.Permissions.Select(userPermission => userPermission.Permission.ToDto())]
        );
    }
}
