namespace OAuth2OpenId.Identity.Models;

public class UserPermission
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = default!;
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = default!;
    public bool IsGranted { get; set; }
}
