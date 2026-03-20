namespace Auth.Domain.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Position { get; set; }
    public string? Department { get; set; }
    public Guid? CompanyId { get; set; }
    public string AuthSource { get; set; } = "Local";
    public List<UserPermission> Permissions { get; set; } = default!;
}