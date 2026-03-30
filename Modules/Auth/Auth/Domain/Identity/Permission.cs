using Shared.DDD;

namespace Auth.Domain.Identity;

public class Permission : Entity<Guid>
{
    public string PermissionCode { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string Module { get; private set; } = default!;

    private Permission() { }

    public static Permission Create(string permissionCode, string displayName, string description, string module)
    {
        return new Permission
        {
            Id = Guid.CreateVersion7(),
            PermissionCode = permissionCode,
            DisplayName = displayName,
            Description = description,
            Module = module
        };
    }

    public void Update(string displayName, string description, string module)
    {
        DisplayName = displayName;
        Description = description;
        Module = module;
    }
}
