namespace OAuth2OpenId.Domain.Identity.Models;

public class RolePermission
{
    public Guid RoleId { get; set; }
    public ApplicationRole Role { get; set; } = default!;
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = default!;
}