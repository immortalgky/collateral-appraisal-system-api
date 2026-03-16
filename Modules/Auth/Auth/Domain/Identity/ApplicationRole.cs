namespace Auth.Domain.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public string Description { get; set; } = default!;
    public List<RolePermission> Permissions { get; set; } = default!;
}