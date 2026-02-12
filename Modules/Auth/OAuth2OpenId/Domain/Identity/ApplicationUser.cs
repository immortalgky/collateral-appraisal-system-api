namespace OAuth2OpenId.Domain.Identity.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Position { get; set; }
    public string? Department { get; set; }
    public List<UserPermission> Permissions { get; set; } = default!;
}