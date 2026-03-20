using Shared.DDD;

namespace Auth.Domain.Identity;

public class Permission : Entity<Guid>
{
    public string PermissionCode { get; set; } = default!;
    public string Description { get; set; } = default!;
}
