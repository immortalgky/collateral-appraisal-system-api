using OAuth2OpenId.Identity.Models;

namespace Auth.Extensions;

public static class DtoExtensions
{
    public static PermissionDto ToDto(this Permission domain)
    {
        return new PermissionDto(domain.Id, domain.PermissionCode, domain.Description);
    }
}
