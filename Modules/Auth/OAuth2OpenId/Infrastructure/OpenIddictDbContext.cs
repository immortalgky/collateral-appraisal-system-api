namespace OAuth2OpenId.Data;

public class OpenIddictDbContext(DbContextOptions<OpenIddictDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Configure the default schema for the database
        builder.HasDefaultSchema("auth");

        // Apply global conventions for the model
        builder.ApplyGlobalConventions();

        // Apply configurations from the current assembly
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Call the base method to ensure any additional configurations are applied
        base.OnModelCreating(builder);
    }
}