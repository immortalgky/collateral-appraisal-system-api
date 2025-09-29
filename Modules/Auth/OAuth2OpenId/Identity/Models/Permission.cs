using Shared.DDD;

namespace OAuth2OpenId.Identity.Models;

public class Permission : Entity<Guid>
{
    public string PermissionCode { get; set; } = default!;
    public string Description { get; set; } = default!;
}
