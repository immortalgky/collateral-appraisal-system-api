using Auth.Domain.Companies;
using Auth.Domain.Groups;
using Auth.Domain.Menu;

namespace Auth.Infrastructure;

public class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupUser> GroupUsers => Set<GroupUser>();
    public DbSet<GroupMonitoring> GroupMonitoring => Set<GroupMonitoring>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<ActivityMenuOverride> ActivityMenuOverrides => Set<ActivityMenuOverride>();

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

        // Global query filter for soft-deleted companies
        builder.Entity<Company>().HasQueryFilter(c => !c.IsDeleted);

        // Global query filter for soft-deleted groups
        builder.Entity<Group>().HasQueryFilter(g => !g.IsDeleted);
    }
}