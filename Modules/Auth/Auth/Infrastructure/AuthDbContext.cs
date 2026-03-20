using Auth.Domain.Companies;

namespace Auth.Infrastructure;

public class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Company> Companies => Set<Company>();

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
    }
}