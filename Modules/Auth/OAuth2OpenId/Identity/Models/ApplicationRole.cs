namespace OAuth2OpenId.Identity.Models;

public class ApplicationRole : IdentityRole<Guid>
{
    public string Description { get; set; } = default!;
    public List<RolePermission> Permissions { get; set; } = default!;
}