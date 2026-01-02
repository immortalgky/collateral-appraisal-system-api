namespace OAuth2OpenId.Domain.Identity.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    public List<UserPermission> Permissions { get; set; } = default!;
}